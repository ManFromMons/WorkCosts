import fs from "node:fs/promises";
import path from "node:path";
import { Agent, CursorAgentError, type ModelSelection } from "@cursor/sdk";

export type ChatLine = {
  role: "user" | "assistant" | "system";
  text: string;
};

export type AgentSessionKind = "chat" | "plan";

type LocalAgent = Awaited<ReturnType<typeof Agent.create>>;

type PlanStore = {
  agentId?: string;
  name?: string;
  startedName?: string | null;
  lines?: ChatLine[];
};

function modelSelection(): ModelSelection {
  const id = process.env.AGENT_TUI_MODEL?.trim() || "composer-2.5";
  return { id };
}

export class AgentSession {
  private agent: LocalAgent | null = null;
  private currentRun: Awaited<ReturnType<LocalAgent["send"]>> | null = null;
  private savedId: string | null = null;
  readonly lines: ChatLine[] = [];
  busy = false;
  error: string | null = null;
  planName = "";
  planStartedName: string | null = null;
  didResume = false;

  constructor(
    private readonly repoRoot: string,
    private readonly onChange: () => void,
    private readonly kind: AgentSessionKind = "chat",
  ) {}

  get sessionPath(): string {
    const file = this.kind === "plan" ? ".plan-session.json" : ".session-id";
    return path.join(this.repoRoot, "tools", "agent-tui", file);
  }

  get isStarted(): boolean {
    return this.agent !== null;
  }

  async loadStored(): Promise<void> {
    if (this.kind !== "plan") {
      return;
    }
    await this.hydrateFromStore();
    this.onChange();
  }

  historyReplay(): string {
    const parts: string[] = [];
    for (const line of this.lines) {
      if (line.role === "system") {
        continue;
      }
      const label = line.role === "user" ? "User" : "Assistant";
      const text = line.text.trim();
      if (!text) {
        continue;
      }
      parts.push(`${label}: ${text.length > 800 ? `${text.slice(0, 799)}…` : text}`);
    }
    const joined = parts.slice(-24).join("\n\n");
    return joined.length <= 8000 ? joined : joined.slice(-8000);
  }

  async ensureStarted(): Promise<void> {
    if (this.agent) {
      return;
    }
    const apiKey = process.env.CURSOR_API_KEY?.trim();
    if (!apiKey) {
      throw new Error("CURSOR_API_KEY is not set. Create a key in Cursor Dashboard → Integrations.");
    }
    const model = modelSelection();
    const saved = await this.readSavedId();
    this.savedId = saved;
    if (this.kind === "plan") {
      await this.hydrateFromStore();
    }
    try {
      if (saved) {
        this.agent = await Agent.resume(saved, {
          apiKey,
          model,
          mode: "agent",
          local: { cwd: this.repoRoot, settingSources: ["project"] },
        });
        this.didResume = true;
        this.push("system", this.kind === "plan"
          ? `Reconnected planning session ${saved} (${model.id})`
          : `Resumed agent ${saved} (${model.id})`);
        await this.persist();
        this.onChange();
        return;
      }
    } catch (err) {
      this.push("system", `Could not resume ${saved}: ${messageOf(err)}`);
      if (this.kind === "plan") {
        this.planStartedName = null;
      }
    }

    this.didResume = false;
    this.agent = await Agent.create({
      apiKey,
      model,
      mode: "agent",
      local: { cwd: this.repoRoot, settingSources: ["project"] },
    });
    const id = this.agent.agentId ?? (this.agent as { id?: string }).id;
    if (id) {
      await this.writeSavedId(id);
    }
    this.push("system", this.kind === "plan"
      ? `Planning agent ready (${model.id}). Enter a name to send /plan-feature.`
      : `Local agent ready (${model.id}). Skills send as /skill-name …`);
    await this.persist();
    this.onChange();
  }

  async send(prompt: string): Promise<void> {
    if (this.busy) {
      throw new Error("An agent run is already in progress.");
    }
    await this.ensureStarted();
    if (!this.agent) {
      throw new Error("Agent did not start.");
    }
    this.busy = true;
    this.error = null;
    this.push("user", prompt);
    this.push("assistant", "");
    this.onChange();
    try {
      const run = await this.agent.send(prompt, {
        model: modelSelection(),
        mode: "agent",
      });
      this.currentRun = run;
      if (typeof run.stream === "function") {
        for await (const event of run.stream()) {
          this.consumeEvent(event);
          this.onChange();
        }
      }
      const result = await run.wait();
      if (result.status === "error") {
        this.error = result.error?.message ?? `Run failed (${result.id ?? "no id"}).`;
        this.push("system", this.error);
      } else if (result.status === "cancelled") {
        this.push("system", "Run cancelled.");
      }
    } catch (err) {
      if (err instanceof CursorAgentError) {
        this.error = `Agent did not start: ${err.message}`;
      } else {
        this.error = messageOf(err);
      }
      this.push("system", this.error);
    } finally {
      this.currentRun = null;
      this.busy = false;
      await this.persist();
      this.onChange();
    }
  }

  async cancel(): Promise<void> {
    const run = this.currentRun;
    if (!run) {
      return;
    }
    try {
      if (run.supports("cancel")) {
        await run.cancel();
        this.push("system", "Cancelled.");
      } else {
        this.push("system", "This run cannot be cancelled.");
      }
    } catch (err) {
      this.push("system", `Cancel failed: ${messageOf(err)}`);
    }
    await this.persist();
    this.onChange();
  }

  async persist(): Promise<void> {
    if (this.kind !== "plan") {
      return;
    }
    const agentId = this.agent?.agentId ?? (this.agent as { id?: string } | null)?.id ?? this.savedId;
    const body: PlanStore = {
      agentId: agentId ?? undefined,
      name: this.planName,
      startedName: this.planStartedName,
      lines: this.lines.slice(-400),
    };
    await fs.writeFile(this.sessionPath, `${JSON.stringify(body, null, 2)}\n`, "utf8");
  }

  async dispose(): Promise<void> {
    await this.persist();
    const agent = this.agent;
    this.agent = null;
    if (!agent) {
      return;
    }
    await agent[Symbol.asyncDispose]();
  }

  private consumeEvent(event: unknown): void {
    const e = event as {
      type?: string;
      message?: { content?: Array<{ type?: string; text?: string }> };
      text?: string;
    };
    const last = this.lines.at(-1);
    const append = (text: string) => {
      if (last && last.role === "assistant") {
        last.text += text;
      } else {
        this.push("assistant", text);
      }
    };
    if (e.type === "assistant" && Array.isArray(e.message?.content)) {
      for (const block of e.message.content) {
        if (block.type === "text" && block.text) {
          append(block.text);
        }
      }
      return;
    }
    if (typeof e.text === "string" && e.text.length > 0) {
      append(e.text);
    }
  }

  private push(role: ChatLine["role"], text: string): void {
    this.lines.push({ role, text });
    if (this.lines.length > 400) {
      this.lines.splice(0, this.lines.length - 400);
    }
  }

  private async hydrateFromStore(): Promise<void> {
    const store = await this.readPlanStore();
    if (!store) {
      return;
    }
    this.planName = this.planName || store.name || "";
    if (this.planStartedName === null) {
      this.planStartedName = store.startedName ?? null;
    }
    if (this.lines.length === 0 && Array.isArray(store.lines) && store.lines.length > 0) {
      for (const line of store.lines) {
        if (line && (line.role === "user" || line.role === "assistant" || line.role === "system") && typeof line.text === "string") {
          this.lines.push({ role: line.role, text: line.text });
        }
      }
    }
  }

  private async readPlanStore(): Promise<PlanStore | null> {
    try {
      const raw = (await fs.readFile(this.sessionPath, "utf8")).trim();
      if (!raw.startsWith("{")) {
        return null;
      }
      return JSON.parse(raw) as PlanStore;
    } catch {
      return null;
    }
  }

  private async readSavedId(): Promise<string | null> {
    if (this.kind === "plan") {
      const store = await this.readPlanStore();
      const id = store?.agentId?.trim();
      return id && id.length > 0 ? id : null;
    }
    try {
      const id = (await fs.readFile(this.sessionPath, "utf8")).trim();
      return id.length > 0 ? id : null;
    } catch {
      return null;
    }
  }

  private async writeSavedId(id: string): Promise<void> {
    this.savedId = id;
    if (this.kind === "plan") {
      await this.persist();
      return;
    }
    await fs.writeFile(this.sessionPath, id, "utf8");
  }
}

function messageOf(err: unknown): string {
  return err instanceof Error ? err.message : String(err);
}
