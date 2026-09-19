import React, { useEffect, useState } from "react";
import { Box, Text, useInput } from "ink";
import { clampCursor, layoutMultiline, rowColToIndex } from "./multiline.ts";

export function MultilineInput(props: {
  value: string;
  onChange: (value: string) => void;
  onSubmit?: (value: string) => void;
  placeholder?: string;
  width: number;
  maxHeight: number;
  focus?: boolean;
  fill?: boolean;
  dismissOnQ?: boolean;
}): React.ReactElement {
  const focus = props.focus !== false;
  const [cursor, setCursor] = useState(() => props.value.length);
  useEffect(() => {
    setCursor((c) => clampCursor(props.value, c));
  }, [props.value]);

  useInput((input, key) => {
    if (!focus) {
      return;
    }
    if (key.tab || (key.ctrl && input === "c") || key.escape) {
      return;
    }
    if (key.ctrl && (key.upArrow || key.downArrow || key.pageUp || key.pageDown)) {
      return;
    }
    if (props.dismissOnQ && input === "q" && !key.ctrl) {
      return;
    }
    if (key.return && !key.shift) {
      props.onSubmit?.(props.value);
      return;
    }
    let next = props.value;
    let nextCursor = cursor;
    if ((key.return && key.shift) || (key.ctrl && input === "j")) {
      next = `${next.slice(0, cursor)}\n${next.slice(cursor)}`;
      nextCursor = cursor + 1;
    } else if (key.leftArrow) {
      nextCursor = Math.max(0, cursor - 1);
    } else if (key.rightArrow) {
      nextCursor = Math.min(next.length, cursor + 1);
    } else if (key.upArrow) {
      const layout = layoutMultiline(next, props.width, cursor);
      nextCursor = rowColToIndex(next, props.width, Math.max(0, layout.cursorRow - 1), layout.cursorCol);
    } else if (key.downArrow) {
      const layout = layoutMultiline(next, props.width, cursor);
      nextCursor = rowColToIndex(next, props.width, layout.cursorRow + 1, layout.cursorCol);
    } else if (key.backspace || key.delete) {
      if (cursor > 0) {
        next = next.slice(0, cursor - 1) + next.slice(cursor);
        nextCursor = cursor - 1;
      }
    } else if (input) {
      const insert = input.replaceAll("\r\n", "\n").replaceAll("\r", "\n");
      next = next.slice(0, cursor) + insert + next.slice(cursor);
      nextCursor = cursor + insert.length;
    } else {
      return;
    }
    nextCursor = clampCursor(next, nextCursor);
    if (next !== props.value) {
      props.onChange(next);
    }
    setCursor(nextCursor);
  }, { isActive: focus });

  const layout = layoutMultiline(props.value, props.width, cursor);
  const height = Math.max(1, Math.min(props.maxHeight, Math.max(1, layout.lines.length)));
  const start = Math.max(0, Math.min(layout.cursorRow - height + 1, Math.max(0, layout.lines.length - height)));
  const visible = layout.lines.slice(start, start + height);
  const bg = props.fill ? "black" : undefined;
  const placeholder = props.placeholder ?? "";

  if (focus && props.value.length === 0 && placeholder.length > 0) {
    return (
      <Box>
        <Text color="cyan" backgroundColor={bg}>{"> "}</Text>
        <Text inverse backgroundColor={bg}>{placeholder.slice(0, 1)}</Text>
        <Text dimColor backgroundColor={bg}>{placeholder.slice(1)}</Text>
      </Box>
    );
  }

  return (
    <Box flexDirection="column">
      {visible.map((line, i) => {
        const abs = start + i;
        const lead = abs === 0 ? "> " : "  ";
        const active = focus && abs === layout.cursorRow;
        return (
          <Box key={abs}>
            <Text color="cyan" backgroundColor={bg}>{lead}</Text>
            <CursorLine text={line} col={active ? layout.cursorCol : -1} fill={props.fill} />
          </Box>
        );
      })}
    </Box>
  );
}

function CursorLine(props: { text: string; col: number; fill?: boolean }): React.ReactElement {
  const bg = props.fill ? "black" : undefined;
  if (props.col < 0) {
    return <Text backgroundColor={bg}>{props.text.length > 0 ? props.text : " "}</Text>;
  }
  const col = Math.min(props.col, props.text.length);
  const ch = col < props.text.length ? props.text[col] : " ";
  return (
    <Text backgroundColor={bg}>
      {props.text.slice(0, col)}
      <Text inverse backgroundColor={bg}>{ch}</Text>
      {props.text.slice(col + 1)}
    </Text>
  );
}
