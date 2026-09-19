import React, { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { Box, Text, useApp, useInput, useStdout } from "ink";
import TextInput from "ink-text-input";
import { MultilineInput } from "./multilineInput.tsx";
import { layoutMultiline } from "./multiline.ts";
import { AgentSession, type ChatLine } from "./agentSession.ts";
import {
  cycleInboxStatus,
  parseInbox,
  setHeadingStatus,
  toggleCheckbox,
  type InboxHeading,
  type ParsedInbox,
} from "./inbox.ts";
import {
  fetchOrigin,
  landToReview,
  loadGitSnapshot,
  loadPickup,
  loadRecentWork,
  loadQueue,
  mergePlanning,
  otherDirtyPaths,
  showOriginMainFile,
  writeWorkingFile,
  type GitSnapshot,
} from "./git.ts";
import { loadSkills, type SkillInfo } from "./skills.ts";
import type { QueueItem } from "./queue.ts";
import { relativeTime, type WorkItem } from "./work.ts";
import fs from "node:fs/promises";
import path from "node:path";

const INBOX_REL = "docs/features/to-review.md";
type Focus = "queue" | "work" | "context" | "chat" | "plan-name";
type Overlay = "none" | "help" | "url" | "confirm-merge" | "confirm-plan-close" | "palette" | "palette-args";
type ContextMode = "story" | "inbox";

function clamp(n: number, min: number, max: number): number {
  if (max < min) {
    return min;
  }
  return Math.max(min, Math.min(max, n));
}

function insertedOnlyChar(prev: string, next: string, ch: string): boolean {
  if (next.length !== prev.length + 1) {
    return false;
  }
  for (let i = 0; i < next.length; i++) {
    if (next[i] === ch && next.slice(0, i) + next.slice(i + 1) === prev) {
      return true;
    }
  }
  return false;
}

function cycleFocus(focus: Focus, planMode: boolean, reverse: boolean): Focus {
  if (planMode) {
    return focus === "plan-name" ? "chat" : "plan-name";
  }
  const order: Focus[] = ["queue", "work", "context", "chat"];
  const current = focus === "plan-name" ? "chat" : focus;
  const i = order.indexOf(current);
  const at = i < 0 ? 0 : i;
  const next = reverse ? (at + order.length - 1) % order.length : (at + 1) % order.length;
  return order[next] ?? "queue";
}

function useFloatInset(marginRatio: number): { left: number; top: number; width: number; height: number } {
  const { stdout } = useStdout();
  const [, setTick] = useState(0);
  useEffect(() => {
    const onResize = () => setTick((n) => n + 1);
    stdout?.on("resize", onResize);
    return () => {
      stdout?.off("resize", onResize);
    };
  }, [stdout]);
  const cols = stdout?.columns ?? 80;
  const rows = stdout?.rows ?? 24;
  const left = Math.max(2, Math.round(cols * marginRatio));
  const top = Math.max(1, Math.round(rows * marginRatio));
  return {
    left,
    top,
    width: Math.max(24, cols - left * 2),
    height: Math.max(12, rows - top * 2),
  };
}

export function App(props: { repoRoot: string }): React.ReactElement {
  const { exit } = useApp();
  const repoRoot = props.repoRoot;
  const [, bump] = useState(0);
  const redraw = useCallback(() => bump((n) => n + 1), []);
  const chatRef = useRef<AgentSession | null>(null);
  const planRef = useRef<AgentSession | null>(null);
  if (!chatRef.current) {
    chatRef.current = new AgentSession(repoRoot, redraw, "chat");
  }
  if (!planRef.current) {
    planRef.current = new AgentSession(repoRoot, redraw, "plan");
  }
  const chatSession = chatRef.current;
  const planSession = planRef.current;
  const [planConnecting, setPlanConnecting] = useState(false);

  const [focus, setFocus] = useState<Focus>("queue");
  const [overlay, setOverlay] = useState<Overlay>("none");
  const [contextMode, setContextMode] = useState<ContextMode>("story");
  const [git, setGit] = useState<GitSnapshot | null>(null);
  const [queue, setQueue] = useState<QueueItem[]>([]);
  const [work, setWork] = useState<WorkItem[]>([]);
  const [pickup, setPickup] = useState("…");
  const [inboxMd, setInboxMd] = useState("");
  const [inbox, setInbox] = useState<ParsedInbox | null>(null);
  const [storyText, setStoryText] = useState("");
  const [skills, setSkills] = useState<SkillInfo[]>([]);
  const [queueIndex, setQueueIndex] = useState(0);
  const [workIndex, setWorkIndex] = useState(0);
  const [activeKebab, setActiveKebab] = useState<string | null>(null);
  const lastLeftRef = useRef<Exclude<Focus, "context" | "chat" | "plan-name">>("queue");
  const livePlanKebabRef = useRef<string | null>(null);
  const [boxIndex, setBoxIndex] = useState(0);
  const [headingIndex, setHeadingIndex] = useState(0);
  const [toast, setToast] = useState("");
  const [chatDraft, setChatDraft] = useState("");
  const [overlayDraft, setOverlayDraft] = useState("");
  const [planMode, setPlanMode] = useState(false);
  const [planName, setPlanName] = useState("");
  const [planStartedName, setPlanStartedName] = useState<string | null>(null);
  const [planDirty, setPlanDirty] = useState(false);
  const [planChatScroll, setPlanChatScroll] = useState(0);
  const [storyScroll, setStoryScroll] = useState(0);
  const { stdout } = useStdout();
  const [, setSizeTick] = useState(0);

  useEffect(() => {
    const onResize = () => setSizeTick((n) => n + 1);
    stdout?.on("resize", onResize);
    return () => {
      stdout?.off("resize", onResize);
    };
  }, [stdout]);
  const [paletteIndex, setPaletteIndex] = useState(0);
  const [pendingSkill, setPendingSkill] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  const selectedQueue = queue[queueIndex] ?? null;
  const selectedWork = work[workIndex] ?? null;
  const selectedKebab = activeKebab
    ?? selectedWork?.kebab
    ?? selectedQueue?.kebab
    ?? null;
  const selected = (selectedKebab ? queue.find((item) => item.kebab === selectedKebab) : null) ?? selectedQueue;
  const heading: InboxHeading | null = useMemo(() => {
    if (!inbox) {
      return null;
    }
    if (contextMode === "inbox") {
      return inbox.headings[headingIndex] ?? inbox.headings.find((h) => h.kebab === selected?.kebab) ?? null;
    }
    return inbox.headings.find((h) => h.kebab === selected?.kebab) ?? null;
  }, [inbox, headingIndex, contextMode, selected]);

  const refresh = useCallback(async (withFetch: boolean) => {
    setLoading(true);
    try {
      if (withFetch) {
        await fetchOrigin(repoRoot);
      }
      const [nextGit, nextQueue, nextPickup, nextInboxMd, nextSkills] = await Promise.all([
        loadGitSnapshot(repoRoot),
        loadQueue(repoRoot),
        loadPickup(repoRoot),
        showOriginMainFile(repoRoot, INBOX_REL),
        loadSkills(repoRoot),
      ]);
      const titles = new Map(nextQueue.items.map((item) => [item.kebab, item.title]));
      const storyStatus = new Map(nextQueue.items.map((item) => [item.kebab, item.status]));
      const inboxStatus = new Map(
        parseInbox(nextInboxMd).headings.map((h) => [h.kebab, h.status]),
      );
      const nextWork = await loadRecentWork(repoRoot, {
        kebabs: [...new Set([...titles.keys(), ...inboxStatus.keys()])],
        titles,
        storyStatus,
        inboxStatus,
        currentBranch: nextGit.branch,
        dirtyPaths: nextGit.dirtyPaths,
      });
      setGit(nextGit);
      setQueue(nextQueue.items);
      setWork(nextWork);
      setPickup(nextPickup);
      setInboxMd(nextInboxMd);
      setInbox(parseInbox(nextInboxMd));
      setSkills(nextSkills);
      setToast(withFetch ? "Fetched origin and reloaded." : "");
    } catch (err) {
      setToast(err instanceof Error ? err.message : String(err));
    } finally {
      setLoading(false);
    }
  }, [repoRoot]);

  useEffect(() => {
    void refresh(true);
    return () => {
      void chatSession.dispose();
      void planSession.dispose();
    };
  }, [chatSession, planSession, refresh]);

  useEffect(() => {
    const kebab = selectedKebab;
    if (!kebab) {
      setStoryText("");
      return;
    }
    const file = path.join(repoRoot, "docs", "features", `${kebab}.md`);
    void fs.readFile(file, "utf8").then(setStoryText).catch(() => setStoryText(`No story file docs/features/${kebab}.md`));
  }, [repoRoot, selectedKebab]);

  useEffect(() => {
    setStoryScroll(0);
  }, [selectedKebab]);

  useEffect(() => {
    if (focus === "queue" && selectedQueue) {
      setActiveKebab(selectedQueue.kebab);
      lastLeftRef.current = "queue";
    }
    if (focus === "work" && selectedWork) {
      setActiveKebab(selectedWork.kebab);
      lastLeftRef.current = "work";
    }
  }, [focus, selectedQueue, selectedWork]);

  const sendChat = useCallback(async (text: string, dest?: "chat" | "plan") => {
    const prompt = text.trim();
    if (!prompt) {
      return;
    }
    setChatDraft("");
    const usePlan = dest === "plan" || (dest !== "chat" && planMode);
    setFocus("chat");
    if (usePlan) {
      setPlanDirty(true);
    }
    try {
      await (usePlan ? planSession : chatSession).send(prompt);
    } catch (err) {
      setToast(err instanceof Error ? err.message : String(err));
    }
  }, [chatSession, planMode, planSession]);

  const startPlan = useCallback(async (name: string) => {
    const trimmed = name.trim();
    if (!trimmed) {
      setToast("Enter a feature name.");
      return;
    }
    setPlanName(trimmed);
    planSession.planName = trimmed;
    if (planStartedName === trimmed) {
      setFocus("chat");
      void planSession.persist();
      return;
    }
    setPlanStartedName(trimmed);
    planSession.planStartedName = trimmed;
    livePlanKebabRef.current = guessKebab(trimmed) || trimmed;
    setPlanDirty(true);
    setFocus("chat");
    const branch = git?.branch;
    if (branch && branch !== "Planning") {
      setToast(`plan-feature writes on Planning (now ${branch}).`);
    }
    await sendChat(
      `/plan-feature ${trimmed}\n\nCreate docs/features/<kebab>.md for this feature. Follow the plan-feature skill: fill every template section, ask numbered questions, do not implement unless I ask. Feature name: ${trimmed}`,
      "plan",
    );
  }, [git?.branch, planSession, planStartedName, sendChat]);

  const landInbox = useCallback(async () => {
    if (!heading) {
      setToast("No inbox heading selected.");
      return;
    }
    const snap = git ?? (await loadGitSnapshot(repoRoot));
    const extra = otherDirtyPaths(snap.dirtyPaths);
    if (extra.length > 0) {
      setToast(`Working tree has other dirty paths: ${extra.join(", ")}`);
      return;
    }
    try {
      await writeWorkingFile(repoRoot, INBOX_REL, inboxMd);
      const result = await landToReview(repoRoot, `to-review: ${heading.kebab} ${heading.status || "update"}`);
      setToast((result.stderr + result.stdout).trim().slice(-400) || `Landed ${heading.kebab}.`);
      await refresh(true);
    } catch (err) {
      setToast(err instanceof Error ? err.message : String(err));
    }
  }, [git, heading, inboxMd, refresh, repoRoot]);

  const closePlanPanel = useCallback(() => {
    planSession.planName = planName;
    planSession.planStartedName = planStartedName;
    void planSession.persist();
    setOverlay("none");
    setPlanMode(false);
    setPlanDirty(false);
    setChatDraft("");
    setFocus(lastLeftRef.current);
  }, [planName, planSession, planStartedName]);

  const openPlanPanel = useCallback((opts?: { kebab?: string; fromWork?: boolean }) => {
    const kebab = opts?.kebab?.trim() ?? "";
    const fromWork = opts?.fromWork === true;
    if (fromWork && !kebab) {
      setToast("No working story selected.");
      return;
    }

    setPlanMode(true);
    setPlanDirty(false);
    setPlanChatScroll(0);

    const alreadyLive = planSession.isStarted;
    const sameLive = Boolean(kebab && alreadyLive && livePlanKebabRef.current === kebab);
    if (fromWork && sameLive) {
      setPlanName(kebab);
      setPlanStartedName(kebab);
      planSession.planName = kebab;
      planSession.planStartedName = kebab;
      setFocus("chat");
      setPlanConnecting(false);
      return;
    }

    setFocus(fromWork ? "chat" : "plan-name");
    setPlanConnecting(true);
    void (async () => {
      try {
        await planSession.loadStored();
        if (kebab) {
          setPlanName(kebab);
          setPlanStartedName(kebab);
          planSession.planName = kebab;
          planSession.planStartedName = kebab;
        } else {
          setPlanName(planSession.planName);
          setPlanStartedName(planSession.planStartedName);
        }
        await planSession.ensureStarted();
        if (kebab) {
          setPlanName(kebab);
          setPlanStartedName(kebab);
          planSession.planName = kebab;
          planSession.planStartedName = kebab;
          await planSession.persist();
          livePlanKebabRef.current = kebab;
          if (fromWork && !planSession.busy) {
            const replay = planSession.didResume ? "" : planSession.historyReplay();
            await sendChat(continuePlanPrompt(kebab, replay), "plan");
          }
        }
      } catch (err) {
        setToast(err instanceof Error ? err.message : String(err));
      } finally {
        setPlanConnecting(false);
      }
    })();
  }, [planSession, sendChat]);

  const requestClosePlan = useCallback(() => {
    if (planDirty || planSession.busy) {
      setOverlay("confirm-plan-close");
      return;
    }
    closePlanPanel();
  }, [closePlanPanel, planDirty, planSession.busy]);

  const storyViewport = Math.max(6, (stdout?.rows ?? 24) - 16);
  const storyWidth = Math.max(16, Math.floor((stdout?.columns ?? 80) * 0.58) - 6);
  const storyLines = wrapText(storyText || "Enter on a queue row to open the story.", storyWidth);
  const storyMax = Math.max(0, storyLines.length - storyViewport);
  const storyOffset = clamp(storyScroll, 0, storyMax);

  useEffect(() => {
    setStoryScroll((s) => clamp(s, 0, storyMax));
  }, [storyMax]);

  useInput((input, key) => {
    if (overlay === "confirm-plan-close") {
      if (key.return || input === "y" || input === "Y") {
        closePlanPanel();
        return;
      }
      setOverlay("none");
      return;
    }
    if (overlay === "url" || overlay === "palette-args") {
      if (key.escape) {
        setOverlay("none");
        setPendingSkill(null);
        setOverlayDraft("");
      }
      return;
    }
    if (overlay === "help") {
      if (key.escape || input === "q" || input === "?") {
        setOverlay("none");
      }
      return;
    }
    if (overlay === "confirm-merge") {
      if (input === "y" || input === "Y") {
        setOverlay("none");
        void (async () => {
          const snap = git ?? (await loadGitSnapshot(repoRoot));
          if (snap.dirty) {
            setToast("Working tree is dirty. Commit or stash before merge-planning.");
            return;
          }
          try {
            const result = await mergePlanning(repoRoot);
            setToast((result.stderr + result.stdout).trim().slice(-400));
            await refresh(true);
          } catch (err) {
            setToast(err instanceof Error ? err.message : String(err));
          }
        })();
      } else if (input === "n" || input === "N" || key.escape) {
        setOverlay("none");
      }
      return;
    }
    if (overlay === "palette") {
      if (key.escape) {
        setOverlay("none");
        return;
      }
      if (key.downArrow || input === "j") {
        setPaletteIndex((i) => clamp(i + 1, 0, Math.max(0, skills.length - 1)));
        return;
      }
      if (key.upArrow || input === "k") {
        setPaletteIndex((i) => clamp(i - 1, 0, Math.max(0, skills.length - 1)));
        return;
      }
      if (key.return) {
        const skill = skills[paletteIndex];
        if (!skill) {
          return;
        }
        setPendingSkill(skill.name);
        setOverlayDraft("");
        setOverlay("palette-args");
      }
      return;
    }

    const typing = focus === "chat" || focus === "plan-name";

    if (key.tab) {
      setFocus((f) => cycleFocus(f, planMode, key.shift));
      return;
    }
    if (input === "h" && !typing) {
      setFocus((f) => {
        if (f === "work") {
          return "queue";
        }
        if (f === "context") {
          return lastLeftRef.current;
        }
        if (f === "chat") {
          return "context";
        }
        return "queue";
      });
      return;
    }
    if (input === "l" && !typing) {
      setFocus((f) => (f === "queue" || f === "work" ? "context" : "chat"));
      return;
    }
    if (input === "?" && !typing) {
      setOverlay("help");
      return;
    }
    if (input === "q" && !key.ctrl) {
      if (planMode) {
        requestClosePlan();
        return;
      }
      if (!typing) {
        void Promise.all([chatSession.dispose(), planSession.dispose()]).finally(() => exit());
        return;
      }
    }
    if (input === ":" && !typing) {
      setPaletteIndex(0);
      setOverlay("palette");
      return;
    }
    if (input === "g" && !typing) {
      void refresh(true);
      return;
    }
    if (input === "p" && !typing) {
      if (focus === "work") {
        openPlanPanel({ kebab: selectedWork?.kebab, fromWork: true });
        return;
      }
      openPlanPanel();
      return;
    }
    if (input === "a" && !typing) {
      setOverlayDraft("");
      setOverlay("url");
      return;
    }
    if (planMode && overlay === "none" && key.ctrl && key.upArrow) {
      setPlanChatScroll((s) => s + 1);
      return;
    }
    if (planMode && overlay === "none" && key.ctrl && key.downArrow) {
      setPlanChatScroll((s) => Math.max(0, s - 1));
      return;
    }
    if (planMode && overlay === "none" && key.ctrl && key.pageUp) {
      setPlanChatScroll((s) => s + 5);
      return;
    }
    if (planMode && overlay === "none" && key.ctrl && key.pageDown) {
      setPlanChatScroll((s) => Math.max(0, s - 5));
      return;
    }

    if (focus === "plan-name") {
      if (key.escape) {
        requestClosePlan();
      }
      return;
    }

    if (focus === "chat") {
      if (key.escape) {
        if (planMode) {
          requestClosePlan();
        } else {
          setFocus(lastLeftRef.current);
        }
        return;
      }
      if (key.ctrl && input === "c") {
        void (planMode ? planSession : chatSession).cancel();
      }
      return;
    }

    if (focus === "queue") {
      if (key.downArrow || input === "j") {
        setQueueIndex((i) => clamp(i + 1, 0, Math.max(0, queue.length - 1)));
        return;
      }
      if (key.upArrow || input === "k") {
        setQueueIndex((i) => clamp(i - 1, 0, Math.max(0, queue.length - 1)));
        return;
      }
      if (key.return) {
        setContextMode("story");
        setFocus("context");
        return;
      }
      if (input === "r") {
        const idx = inbox?.headings.findIndex((h) => h.kebab === selected?.kebab) ?? -1;
        if (idx >= 0) {
          setHeadingIndex(idx);
        }
        setContextMode("inbox");
        setBoxIndex(0);
        setFocus("context");
        return;
      }
      if (input === "i" && selected) {
        void sendChat(`/start-implement ${selected.seq === "?" ? selected.kebab : selected.seq}`);
        return;
      }
      if (input === "n") {
        if (pickup === "QUEUE_EMPTY") {
          setToast("QUEUE_EMPTY");
          return;
        }
        void sendChat("/pickup-next-feature");
        return;
      }
      if (input === "m") {
        setOverlay("confirm-merge");
      }
      return;
    }

    if (focus === "work") {
      if (key.downArrow || input === "j") {
        setWorkIndex((i) => clamp(i + 1, 0, Math.max(0, work.length - 1)));
        return;
      }
      if (key.upArrow || input === "k") {
        setWorkIndex((i) => clamp(i - 1, 0, Math.max(0, work.length - 1)));
        return;
      }
      if (key.return) {
        setContextMode("story");
        setFocus("context");
        return;
      }
      if (input === "r") {
        const kebab = selectedWork?.kebab ?? selectedKebab;
        const idx = inbox?.headings.findIndex((h) => h.kebab === kebab) ?? -1;
        if (idx >= 0) {
          setHeadingIndex(idx);
        }
        setContextMode("inbox");
        setBoxIndex(0);
        setFocus("context");
        return;
      }
      if (input === "i") {
        const kebab = selectedWork?.kebab ?? selectedKebab;
        if (kebab) {
          const row = queue.find((item) => item.kebab === kebab);
          void sendChat(`/start-implement ${row && row.seq !== "?" ? row.seq : kebab}`);
        }
        return;
      }
      if (input === "n") {
        if (pickup === "QUEUE_EMPTY") {
          setToast("QUEUE_EMPTY");
          return;
        }
        void sendChat("/pickup-next-feature");
        return;
      }
      if (input === "m") {
        setOverlay("confirm-merge");
      }
      return;
    }

    if (focus === "context") {
      if (key.escape) {
        setFocus(lastLeftRef.current);
        return;
      }
      if (input === "r") {
        const idx = inbox?.headings.findIndex((h) => h.kebab === selected?.kebab) ?? -1;
        if (idx >= 0) {
          setHeadingIndex(idx);
        }
        setContextMode("inbox");
        setBoxIndex(0);
        return;
      }
      if (contextMode === "story") {
        const page = Math.max(1, storyViewport - 1);
        const half = Math.max(1, Math.floor(storyViewport / 2));
        if (key.downArrow || input === "j") {
          setStoryScroll((s) => clamp(s + 1, 0, storyMax));
          return;
        }
        if (key.upArrow || input === "k") {
          setStoryScroll((s) => clamp(s - 1, 0, storyMax));
          return;
        }
        if (key.pageDown || input === " ") {
          setStoryScroll((s) => clamp(s + page, 0, storyMax));
          return;
        }
        if (key.pageUp) {
          setStoryScroll((s) => clamp(s - page, 0, storyMax));
          return;
        }
        if (input === "G") {
          setStoryScroll(storyMax);
          return;
        }
        if (key.ctrl && input === "d") {
          setStoryScroll((s) => clamp(s + half, 0, storyMax));
          return;
        }
        if (key.ctrl && input === "u") {
          setStoryScroll((s) => clamp(s - half, 0, storyMax));
          return;
        }
      }
      if (contextMode === "inbox") {
        if (key.downArrow || input === "j") {
          if (heading && boxIndex < heading.checkboxes.length - 1) {
            setBoxIndex((i) => i + 1);
          } else if (inbox && headingIndex < inbox.headings.length - 1) {
            setHeadingIndex((i) => i + 1);
            setBoxIndex(0);
          }
          return;
        }
        if (key.upArrow || input === "k") {
          if (boxIndex > 0) {
            setBoxIndex((i) => i - 1);
          } else if (headingIndex > 0) {
            const prev = inbox?.headings[headingIndex - 1];
            setHeadingIndex((i) => i - 1);
            setBoxIndex(Math.max(0, (prev?.checkboxes.length ?? 1) - 1));
          }
          return;
        }
        if (input === " ") {
          if (!heading) {
            return;
          }
          try {
            const next = toggleCheckbox(inboxMd, heading.kebab, boxIndex);
            setInboxMd(next);
            setInbox(parseInbox(next));
          } catch (err) {
            setToast(err instanceof Error ? err.message : String(err));
          }
          return;
        }
        if (input === "s") {
          if (!heading) {
            return;
          }
          try {
            const next = setHeadingStatus(inboxMd, heading.kebab, cycleInboxStatus(heading.status));
            setInboxMd(next);
            setInbox(parseInbox(next));
          } catch (err) {
            setToast(err instanceof Error ? err.message : String(err));
          }
          return;
        }
        if (input === "L") {
          void landInbox();
        }
      }
    }
  });

  const footer = footerFor(focus, overlay, contextMode, planMode);
  const float = useFloatInset(0.15);
  const rows = stdout?.rows ?? 24;
  const dockChatWidth = Math.max(20, (stdout?.columns ?? 80) - 6);
  const chatFieldWidth = Math.max(8, dockChatWidth - 2);
  const maxChatInput = Math.max(1, Math.min(12, Math.floor(rows / 3)));
  const chatFocused = !planMode && focus === "chat" && overlay === "none";
  const chatInputHeight = chatFocused
    ? clamp(layoutMultiline(chatDraft, chatFieldWidth, chatDraft.length).lines.length, 1, maxChatInput)
    : 0;
  const chatTranscriptHeight = Math.max(2, 7 - Math.max(0, chatInputHeight - 1));
  const chatPanelHeight = 4 + chatTranscriptHeight + chatInputHeight;
  const dockChatRows = chatDisplayRows(chatSession.lines, dockChatWidth).slice(-chatTranscriptHeight);
  const planInnerHeight = Math.max(1, float.height - 2);
  const planChatWidth = Math.max(20, float.width - 6);
  const planFieldWidth = Math.max(8, planChatWidth - 2);
  const maxPlanInput = Math.max(1, Math.min(10, Math.floor(planInnerHeight / 3)));
  const planInputHeight = focus === "chat" && overlay === "none"
    ? clamp(layoutMultiline(chatDraft, planFieldWidth, chatDraft.length).lines.length, 1, maxPlanInput)
    : 1;
  const planTopChrome = 3;
  const planBottomReserve = 2 + planInputHeight + (overlay === "confirm-plan-close" ? 5 : 0);
  const planChatHeight = Math.max(3, planInnerHeight - planTopChrome - planBottomReserve);
  const planAllRows = chatDisplayRows(planSession.lines, planChatWidth);
  const planMaxScroll = Math.max(0, planAllRows.length - planChatHeight);
  const planOffset = clamp(planChatScroll, 0, planMaxScroll);
  const planChatStart = Math.max(0, planAllRows.length - planChatHeight - planOffset);
  const planChatRows = planAllRows.slice(planChatStart, planChatStart + planChatHeight);

  useEffect(() => {
    setPlanChatScroll((s) => clamp(s, 0, planMaxScroll));
  }, [planMaxScroll]);

  return (
    <Box flexDirection="column" width="100%" height="100%" position="relative">
      <StatusBar git={git} pickup={pickup} loading={loading} toast={toast} />
      <Box flexGrow={1} flexDirection="row">
        <Box flexDirection="column" width="42%" flexGrow={0}>
          <Panel title="Queue" focused={focus === "queue"} height={12} width="100%">
            {queue.length === 0 ? (
              <Text dimColor>{loading ? "Loading…" : "No stories."}</Text>
            ) : (
              windowed(queue, queueIndex, 6).map((item, offset) => {
                const i = windowStart(queue.length, queueIndex, 6) + offset;
                return (
                  <Text
                    key={`${item.seq}-${item.kebab}`}
                    inverse={i === queueIndex}
                    color={item.startable ? "green" : item.status === "done" ? "gray" : undefined}
                  >
                    {`${item.indent.replace(/[|]/g, " ").slice(-8)}${item.startable ? "*" : " "}[${item.seq}] ${item.kebab} (${item.status})`}
                  </Text>
                );
              })
            )}
          </Panel>
          <Panel title="Working" focused={focus === "work"} width="100%">
            {work.length === 0 ? (
              <Text dimColor>{loading ? "Loading…" : "No active stories."}</Text>
            ) : (
              windowed(work, workIndex, 8).map((item, offset) => {
                const i = windowStart(work.length, workIndex, 8) + offset;
                return (
                  <Text
                    key={item.kebab}
                    inverse={i === workIndex}
                    color={item.current ? "green" : item.source === "dirty" ? "yellow" : undefined}
                  >
                    {`${item.current ? "*" : " "}${relativeTime(item.atMs)}  ${item.kebab}${item.branch ? `  ${item.branch}` : item.source === "dirty" ? "  dirty" : ""}`}
                  </Text>
                );
              })
            )}
          </Panel>
        </Box>
        <Panel title={contextMode === "inbox" ? "Inbox" : "Story"} focused={focus === "context"} width="58%">
          {contextMode === "inbox" ? (
            <InboxView inbox={inbox} heading={heading} boxIndex={boxIndex} />
          ) : (
            <StoryView
              lines={storyLines}
              offset={storyOffset}
              viewport={storyViewport}
            />
          )}
        </Panel>
      </Box>
      <Panel title={chatSession.busy ? "Chat (running)" : "Chat"} focused={!planMode && focus === "chat"} height={chatPanelHeight}>
        {dockChatRows.length === 0 ? (
          <Text dimColor>Type a message, or p to plan a feature, a for add-source, : for skills.</Text>
        ) : (
          dockChatRows.map((row, i) => (
            <Text key={i} color={row.color}>
              {row.text}
            </Text>
          ))
        )}
        <BusyLine busy={chatSession.busy} />
        {focus === "chat" && overlay === "none" && !planMode ? (
          <MultilineInput
            value={chatDraft}
            onChange={setChatDraft}
            onSubmit={(value) => {
              void sendChat(value);
            }}
            placeholder="message or /skill-name …"
            width={chatFieldWidth}
            maxHeight={maxChatInput}
          />
        ) : null}
      </Panel>
      <Box>
        <Text backgroundColor="blue" color="white">
          {` ${footer} `}
        </Text>
      </Box>
      {planMode ? (
        <Box
          position="absolute"
          marginTop={float.top}
          marginLeft={float.left}
          width={float.width}
          height={float.height}
          flexDirection="column"
          borderStyle="double"
          borderColor={focus === "plan-name" || focus === "chat" ? "cyan" : "yellow"}
          overflow="hidden"
        >
          <SolidFill width={Math.max(1, float.width - 2)} height={Math.max(1, float.height - 2)} />
          <Box flexDirection="column" paddingX={1} height={planInnerHeight} overflow="hidden">
          <Box flexShrink={0} flexDirection="column">
          <Text color="cyan" bold backgroundColor="black">
            {`${planSession.busy ? "Plan Feature (running)" : planConnecting ? "Plan Feature (connecting)" : "Plan Feature"}${planDirty || planSession.busy ? " *" : ""}${planOffset > 0 ? `  ↑${planOffset}` : ""}`}
          </Text>
          <Box>
            <Text color={focus === "plan-name" && overlay === "none" ? "cyan" : undefined} backgroundColor="black">{"Name: "}</Text>
            {focus === "plan-name" && overlay === "none" ? (
              <TextInput
                value={planName}
                onChange={(value) => {
                  if (insertedOnlyChar(planName, value, "q")) {
                    return;
                  }
                  setPlanName(value);
                  planSession.planName = value;
                  setPlanDirty(true);
                }}
                onSubmit={(value) => {
                  void startPlan(value);
                }}
                placeholder="feature name"
              />
            ) : (
              <Text backgroundColor="black">{planName || "…"}</Text>
            )}
          </Box>
          <Text dimColor backgroundColor="black">
            {planName.trim()
              ? `docs/features/${guessKebab(planName)}.md  — chat below to spec it (Planning branch)`
              : "Type a feature name and Enter to start /plan-feature"}
          </Text>
          </Box>
          <Box height={planChatHeight} flexShrink={0} flexDirection="column" overflow="hidden">
            {planChatRows.length === 0 ? (
              <Text dimColor backgroundColor="black">
                {planConnecting
                  ? "Loading stored planning chat, then reconnecting the agent…"
                  : "The planning conversation is stored. Open this panel again to reconnect."}
              </Text>
            ) : (
              planChatRows.map((row, i) => (
                <Text key={i} backgroundColor="black" color={row.color}>
                  {row.text}
                </Text>
              ))
            )}
          </Box>
          <Box flexShrink={0} flexDirection="column">
            <Text backgroundColor="black">{" "}</Text>
            <BusyLine busy={planSession.busy} fill />
            {focus === "chat" && overlay === "none" ? (
              <MultilineInput
                value={chatDraft}
                onChange={(value) => {
                  setChatDraft(value);
                  setPlanDirty(true);
                }}
                onSubmit={(value) => {
                  void sendChat(value);
                }}
                placeholder="answer questions / refine the spec"
                width={planFieldWidth}
                maxHeight={maxPlanInput}
                fill
                dismissOnQ
              />
            ) : overlay === "none" ? (
              <Text dimColor backgroundColor="black">{"> tab or enter name to chat"}</Text>
            ) : null}
            {overlay === "confirm-plan-close" ? (
              <Box flexDirection="column" borderStyle="double" borderColor="yellow" paddingX={1}>
                <Text bold backgroundColor="black">Close Plan Feature?</Text>
                <Text backgroundColor="black">This panel has unsaved work.</Text>
                <Text dimColor backgroundColor="black">y or Enter = close · any other key = stay</Text>
              </Box>
            ) : null}
          </Box>
          </Box>
        </Box>
      ) : null}
      {overlay === "help" ? <Help /> : null}
      {overlay === "confirm-merge" ? (
        <Modal title="merge-planning">
          <Text>Working tree must be clean. Run Merge-PlanningToMain.ps1? [y/n]</Text>
        </Modal>
      ) : null}
      {overlay === "url" ? (
        <Modal title="start-add-source">
          <Text>Product URL:</Text>
          <TextInput
            value={overlayDraft}
            onChange={setOverlayDraft}
            onSubmit={(value) => {
              setOverlay("none");
              const url = value.trim();
              if (url) {
                void sendChat(`/start-add-source ${url}`);
              }
            }}
            placeholder="https://…"
          />
        </Modal>
      ) : null}
      {overlay === "palette" ? (
        <Modal title="skills">
          {skills.map((skill, i) => (
            <Text key={skill.name} inverse={i === paletteIndex} color={skill.invokeOnly ? "yellow" : undefined}>
              {`/${skill.name}${skill.invokeOnly ? " (invoke-only)" : ""}  ${truncate(skill.description, 80)}`}
            </Text>
          ))}
        </Modal>
      ) : null}
      {overlay === "palette-args" ? (
        <Modal title={`/${pendingSkill}`}>
          <Text>Extra arguments (Enter to send, Esc via empty + Ctrl is not needed — submit empty for none):</Text>
          <TextInput
            value={overlayDraft}
            onChange={setOverlayDraft}
            onSubmit={(value) => {
              const name = pendingSkill;
              setPendingSkill(null);
              setOverlay("none");
              if (!name) {
                return;
              }
              const extra = value.trim();
              void sendChat(extra ? `/${name} ${extra}` : `/${name}`);
            }}
            placeholder="optional args"
          />
        </Modal>
      ) : null}
    </Box>
  );
}

function StatusBar(props: {
  git: GitSnapshot | null;
  pickup: string;
  loading: boolean;
  toast: string;
}): React.ReactElement {
  const branch = props.git?.short ?? props.git?.branch ?? "…";
  const dirty = props.git?.dirty ? "dirty" : "clean";
  const vs = props.git?.mainVsPlanning ? `  ${props.git.mainVsPlanning}` : "";
  const key = process.env.CURSOR_API_KEY?.trim() ? "" : "  no CURSOR_API_KEY";
  const toast = props.toast ? ` | ${truncate(props.toast.replace(/\s+/g, " "), 80)}` : "";
  return (
    <Box>
      <Text backgroundColor="yellow" color="black">
        {` ${branch} ${dirty}${vs}  pickup:${props.pickup}${key}${props.loading ? "  loading" : ""}${toast} `}
      </Text>
    </Box>
  );
}

function Panel(props: {
  title: string;
  focused: boolean;
  width?: number | string;
  height?: number | string;
  children: React.ReactNode;
}): React.ReactElement {
  return (
    <Box
      flexDirection="column"
      borderStyle="single"
      borderColor={props.focused ? "cyan" : "gray"}
      width={props.width}
      height={props.height}
      flexGrow={props.height ? 0 : 1}
      paddingX={1}
    >
      <Text color={props.focused ? "cyan" : "gray"}>{props.title}</Text>
      {props.children}
    </Box>
  );
}

function InboxView(props: {
  inbox: ParsedInbox | null;
  heading: InboxHeading | null;
  boxIndex: number;
}): React.ReactElement {
  if (!props.heading) {
    return <Text dimColor>r on a queue row to open its origin/main inbox heading.</Text>;
  }
  return (
    <Box flexDirection="column">
      <Text bold>{`${props.heading.kebab}  (${props.heading.status || "no Status"})`}</Text>
      {props.heading.checkboxes.length === 0 ? (
        <Text dimColor>No checkboxes.</Text>
      ) : (
        props.heading.checkboxes.map((box) => (
          <Text key={box.index} inverse={box.index === props.boxIndex} color={box.checked ? "green" : "white"}>
            {`${box.checked ? "[x]" : "[ ]"} ${truncate(box.text, 90)}`}
          </Text>
        ))
      )}
      <Text dimColor>space tick  s cycle Status  L land on main</Text>
    </Box>
  );
}

function StoryView(props: { lines: string[]; offset: number; viewport: number }): React.ReactElement {
  const total = props.lines.length;
  const height = Math.max(1, props.viewport);
  const visible = props.lines.slice(props.offset, props.offset + height);
  while (visible.length < height) {
    visible.push("");
  }
  const thumb = scrollThumb(props.offset, height, total, height);
  return (
    <Box flexDirection="row">
      <Box flexDirection="column" flexGrow={1}>
        {visible.map((line, i) => (
          <Text key={i}>{line.length > 0 ? line : " "}</Text>
        ))}
      </Box>
      <Box flexDirection="column" width={1} flexShrink={0}>
        {thumb.map((on, i) => (
          <Text key={i} color={on ? "cyan" : "gray"} dimColor={!on}>
            {on ? "█" : "░"}
          </Text>
        ))}
      </Box>
    </Box>
  );
}

function Help(): React.ReactElement {
  return (
    <Modal title="help">
      <Text>tab / shift-tab / h l focus  j k arrows move  q quit  g fetch+reload  : skills</Text>
      <Text>Queue: enter story  r inbox  i implement  n pickup  p plan-feature  a add-source  m merge-planning</Text>
      <Text>Working: p continues that story's plan — history shows first, then state/next unless same live plan</Text>
      <Text>Story: j/k scroll  space/PgDn  PgUp  G end  scrollbar on the right</Text>
      <Text>Plan Feature: q/esc close  ctrl-up/down scroll transcript  y/enter close confirm</Text>
      <Text>Working: newest active stories first (feature branches, dirty files, unfinished status)</Text>
      <Text>Inbox: space toggle checkbox  s cycle Status  L Update-ToReviewOnMain.ps1</Text>
      <Text>Chat: type wraps  enter send  shift-enter newline  esc back  ctrl+c cancel run</Text>
      <Text>Canonical inbox is origin/main. Land only if no other dirty files.</Text>
    </Modal>
  );
}

function Modal(props: { title: string; children: React.ReactNode }): React.ReactElement {
  return (
    <Box
      flexDirection="column"
      borderStyle="double"
      borderColor="yellow"
      paddingX={1}
      width="90%"
    >
      <Text color="yellow">{props.title}</Text>
      {props.children}
    </Box>
  );
}

function footerFor(focus: Focus, overlay: Overlay, contextMode: ContextMode, planMode: boolean): string {
  if (overlay === "help") {
    return "esc close help";
  }
  if (overlay === "confirm-plan-close") {
    return "y or enter close panel  any other key stay";
  }
  if (overlay === "confirm-merge") {
    return "y run  n cancel";
  }
  if (overlay === "url" || overlay === "palette-args") {
    return "enter submit  esc cancel";
  }
  if (overlay === "palette") {
    return "j/k select  enter args  esc cancel";
  }
  if (focus === "plan-name") {
    return "enter start /plan-feature  tab/S-tab chat  ctrl-up/down scroll  q/esc close";
  }
  if (focus === "queue") {
    return "j/k  tab/S-tab  l story  enter  r inbox  i implement  n next  p plan  a add-source  m merge  :  g  ?  q";
  }
  if (focus === "work") {
    return "j/k newest-first  h queue  l story  enter  r inbox  i implement  p continue-plan  a add-source  tab/S-tab  ?  q";
  }
  if (focus === "context" && contextMode === "inbox") {
    return "j/k boxes  space tick  s Status  L land  h left  l chat  p plan  esc";
  }
  if (focus === "context") {
    return "j/k scroll  space/PgDn  PgUp  G end  r inbox  h left  l chat  p plan  esc";
  }
  if (planMode) {
    return "ctrl-up/down scroll  enter send  shift-enter newline  q/esc close  tab/S-tab";
  }
  return "enter send  shift-enter newline  esc back  ctrl+c cancel  tab/S-tab";
}

const SPIN_FRAMES = "|/-\\";
const BUSY_TAIL = " working…  ctrl+c cancel";
const BUSY_WIDTH = 1 + BUSY_TAIL.length;
const READY_LABEL = "ready".padEnd(BUSY_WIDTH);

const spinListeners = new Set<() => void>();
let spinTimer: ReturnType<typeof setInterval> | null = null;

function subscribeSpin(onTick: () => void): () => void {
  spinListeners.add(onTick);
  if (!spinTimer) {
    spinTimer = setInterval(() => {
      for (const listener of spinListeners) {
        listener();
      }
    }, 120);
  }
  return () => {
    spinListeners.delete(onTick);
    if (spinListeners.size === 0 && spinTimer) {
      clearInterval(spinTimer);
      spinTimer = null;
    }
  };
}

function BusyLine(props: { busy: boolean; fill?: boolean }): React.ReactElement {
  const [spin, setSpin] = useState(0);
  useEffect(() => {
    if (!props.busy) {
      return;
    }
    return subscribeSpin(() => setSpin((n) => n + 1));
  }, [props.busy]);
  const frame = SPIN_FRAMES[spin % 4];
  const label = props.busy ? `${frame}${BUSY_TAIL}` : READY_LABEL;
  return (
    <Text
      color={props.busy ? "yellow" : "gray"}
      dimColor={!props.busy}
      backgroundColor={props.fill ? "black" : undefined}
    >
      {label}
    </Text>
  );
}

function SolidFill(props: { width: number; height: number }): React.ReactElement {
  const line = " ".repeat(Math.max(1, props.width));
  const rows = Array.from({ length: Math.max(1, props.height) }, (_, i) => i);
  return (
    <Box position="absolute" flexDirection="column">
      {rows.map((i) => (
        <Text key={i} backgroundColor="black">{line}</Text>
      ))}
    </Box>
  );
}

function continuePlanPrompt(kebab: string, replay = ""): string {
  const spec = `Read docs/features/${kebab}.md. What state are we in? What can we do next?`;
  const history = replay.trim();
  if (history) {
    return `Continue planning feature ${kebab}. The live thread could not be resumed; use this previous planning conversation as history:\n\n${history}\n\n${spec}`;
  }
  return `Continue planning feature ${kebab}. Use the previous planning conversation already in this thread. ${spec}`;
}

function guessKebab(name: string): string {
  return name
    .trim()
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-+|-+$/g, "");
}

function chatDisplayRows(lines: ChatLine[], width: number): Array<{ color?: string; text: string }> {
  const rows: Array<{ color?: string; text: string }> = [];
  for (const line of lines) {
    const color = line.role === "user" ? "cyan" : line.role === "system" ? "yellow" : undefined;
    const prefix = line.role === "user" ? "> " : line.role === "system" ? "! " : "  ";
    const wrapped = wrapText(line.text, Math.max(8, width - prefix.length));
    wrapped.forEach((row, i) => {
      const lead = i === 0 ? prefix : " ".repeat(prefix.length);
      rows.push({ color, text: `${lead}${row.length > 0 ? row : " "}` });
    });
  }
  return rows;
}

function scrollThumb(offset: number, viewport: number, total: number, height: number): boolean[] {
  const track = Math.max(1, height);
  if (total <= viewport) {
    return Array.from({ length: track }, () => true);
  }
  const thumb = Math.max(1, Math.round((viewport / total) * track));
  const maxStart = Math.max(0, track - thumb);
  const start = Math.round((offset / Math.max(1, total - viewport)) * maxStart);
  return Array.from({ length: track }, (_, i) => i >= start && i < start + thumb);
}

function wrapText(text: string, width: number): string[] {
  const w = Math.max(8, width);
  const out: string[] = [];
  for (const para of text.replace(/\r\n/g, "\n").split("\n")) {
    if (para.length === 0) {
      out.push("");
      continue;
    }
    let rest = para;
    while (rest.length > w) {
      const slice = rest.slice(0, w);
      const sp = slice.lastIndexOf(" ");
      const cut = sp >= Math.floor(w * 0.45) ? sp + 1 : w;
      out.push(rest.slice(0, cut).trimEnd());
      rest = rest.slice(cut).trimStart();
    }
    out.push(rest);
  }
  return out.length > 0 ? out : [""];
}

function truncate(text: string, max: number): string {
  const flat = text.replace(/\s+/g, " ").trim();
  return flat.length <= max ? flat : `${flat.slice(0, max - 1)}…`;
}

function windowStart(length: number, index: number, size: number): number {
  if (length <= size) {
    return 0;
  }
  return Math.min(Math.max(0, index - Math.floor((size - 1) / 2)), length - size);
}

function windowed<T>(items: T[], index: number, size: number): T[] {
  const start = windowStart(items.length, index, size);
  return items.slice(start, start + size);
}
