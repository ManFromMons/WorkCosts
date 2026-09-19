import { spawn } from "node:child_process";

export type CommandResult = {
  code: number;
  stdout: string;
  stderr: string;
};

export function runCommand(
  command: string,
  args: string[],
  cwd: string,
  timeoutMs = 120_000,
): Promise<CommandResult> {
  return new Promise((resolve, reject) => {
    const child = spawn(command, args, {
      cwd,
      windowsHide: true,
      env: { ...process.env },
    });
    let stdout = "";
    let stderr = "";
    const timer = setTimeout(() => {
      child.kill();
      reject(new Error(`${command} timed out after ${timeoutMs}ms`));
    }, timeoutMs);
    child.stdout.on("data", (chunk: Buffer) => {
      stdout += chunk.toString("utf8");
    });
    child.stderr.on("data", (chunk: Buffer) => {
      stderr += chunk.toString("utf8");
    });
    child.on("error", (err) => {
      clearTimeout(timer);
      reject(err);
    });
    child.on("close", (code) => {
      clearTimeout(timer);
      resolve({ code: code ?? 1, stdout, stderr });
    });
  });
}

export async function runGit(args: string[], cwd: string): Promise<CommandResult> {
  return runCommand("git", args, cwd);
}

export async function runPowershell(scriptRel: string, extraArgs: string[], cwd: string): Promise<CommandResult> {
  return runCommand(
    "powershell.exe",
    ["-NoProfile", "-ExecutionPolicy", "Bypass", "-File", scriptRel, ...extraArgs],
    cwd,
    180_000,
  );
}
