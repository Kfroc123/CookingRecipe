import { C, bg, title, panel, label } from "./theme.mjs";

export async function slide02(presentation, ctx) {
  const slide = presentation.slides.add();
  bg(slide, ctx, 2);
  title(slide, ctx, "Problem statement", "People have ingredients, but not always a clear meal idea.");

  const cards = [
    ["Unclear choices", "Users may have rice, tomato, onion, or beans at home but still struggle to decide what to cook."],
    ["Local food gap", "Many recipe platforms show mostly foreign meals and do not prioritize Nigerian dishes."],
    ["Time pressure", "Students, families, and home cooks need quick suggestions without searching many websites."],
  ];

  cards.forEach(([head, body], i) => {
    const x = 76 + i * 384;
    panel(slide, ctx, x, 250, 334, 278);
    ctx.addShape(slide, { x: x + 28, y: 282, w: 46, h: 46, fill: [C.red, C.gold, C.green][i], line: ctx.line("#00000000", 0) });
    label(slide, ctx, head, x + 92, 282, 210, 36, { fontSize: 24, bold: true });
    label(slide, ctx, body, x + 28, 350, 276, 130, { fontSize: 21, color: "#33443D" });
  });

  label(
    slide,
    ctx,
    "Why it matters: the project turns available ingredients into practical recipe options and gives Nigerian meals stronger visibility.",
    92,
    582,
    1040,
    46,
    { fontSize: 23, bold: true, color: C.cream, align: "center" },
  );

  return slide;
}
