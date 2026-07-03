export const C = {
  ink: "#10251F",
  ink2: "#18362D",
  cream: "#F6F1E7",
  paper: "#FFF9EF",
  green: "#3A8F62",
  gold: "#F2C94C",
  red: "#E76F51",
  blue: "#4EA5D9",
  muted: "#9CB5A9",
  darkText: "#17312A",
};

export function bg(slide, ctx, page) {
  ctx.addShape(slide, { x: 0, y: 0, w: 1280, h: 720, fill: C.ink });
  ctx.addShape(slide, { x: 40, y: 42, w: 1200, h: 636, fill: "#00000000", line: ctx.line("#315348", 1) });
  ctx.addText(slide, {
    text: String(page).padStart(2, "0"),
    x: 1160,
    y: 644,
    w: 54,
    h: 22,
    fontSize: 12,
    color: C.muted,
    align: "right",
  });
}

export function title(slide, ctx, kicker, claim) {
  ctx.addText(slide, {
    text: kicker.toUpperCase(),
    name: "kicker-label",
    x: 72,
    y: 58,
    w: 420,
    h: 28,
    fontSize: 14,
    bold: true,
    color: C.gold,
    typeface: ctx.fonts.body,
    valign: "mid",
  });
  ctx.addText(slide, {
    text: claim,
    x: 72,
    y: 92,
    w: 880,
    h: 82,
    fontSize: 38,
    bold: true,
    color: C.cream,
    typeface: ctx.fonts.title,
  });
}

export function panel(slide, ctx, x, y, w, h, fill = C.paper, line = "#D8CDBA") {
  return ctx.addShape(slide, {
    x,
    y,
    w,
    h,
    fill,
    line: ctx.line(line, 1),
  });
}

export function label(slide, ctx, text, x, y, w, h, opts = {}) {
  return ctx.addText(slide, {
    text,
    x,
    y,
    w,
    h,
    fontSize: opts.fontSize ?? 20,
    bold: opts.bold ?? false,
    color: opts.color ?? C.darkText,
    typeface: opts.typeface ?? ctx.fonts.body,
    align: opts.align ?? "left",
    valign: opts.valign ?? "top",
    fill: opts.fill ?? "#00000000",
    line: opts.line ?? ctx.line("#00000000", 0),
    insets: opts.insets ?? { left: 8, right: 8, top: 6, bottom: 6 },
  });
}

export function pill(slide, ctx, text, x, y, w, color) {
  ctx.addShape(slide, { x, y, w, h: 34, fill: color, line: ctx.line("#00000000", 0) });
  label(slide, ctx, text, x + 10, y + 5, w - 20, 22, {
    fontSize: 13,
    bold: true,
    color: "#FFFFFF",
    align: "center",
    valign: "mid",
    insets: { left: 0, right: 0, top: 0, bottom: 0 },
  });
}

export function bullet(slide, ctx, text, x, y, w, color = C.green) {
  ctx.addShape(slide, { x, y: y + 8, w: 9, h: 9, fill: color, line: ctx.line("#00000000", 0) });
  label(slide, ctx, text, x + 24, y, w, 44, { fontSize: 20, color: C.cream });
}
