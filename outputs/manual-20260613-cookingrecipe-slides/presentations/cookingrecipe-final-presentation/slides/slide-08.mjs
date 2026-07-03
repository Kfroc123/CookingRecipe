import { C, bg, label, panel } from "./theme.mjs";

export async function slide08(presentation, ctx) {
  const slide = presentation.slides.add();
  bg(slide, ctx, 8);

  ctx.addText(slide, {
    text: "Conclusion",
    x: 78,
    y: 82,
    w: 520,
    h: 72,
    fontSize: 54,
    bold: true,
    color: C.cream,
    typeface: ctx.fonts.title,
  });
  ctx.addText(slide, {
    text: "CookingRecipe connected data, APIs, configuration, and user actions into one working backend project.",
    x: 82,
    y: 162,
    w: 920,
    h: 74,
    fontSize: 26,
    color: "#CFE0D7",
  });

  panel(slide, ctx, 82, 298, 470, 220);
  label(slide, ctx, "What I learned", 112, 328, 250, 32, { fontSize: 25, bold: true });
  label(slide, ctx, "Building real Web APIs\nConnecting external services\nUsing environment variables\nHandling CORS and API testing", 112, 382, 380, 96, {
    fontSize: 21,
    color: "#33443D",
  });

  panel(slide, ctx, 620, 298, 470, 220, "#FFF2C6");
  label(slide, ctx, "Possible improvements", 650, 328, 300, 32, { fontSize: 25, bold: true });
  label(slide, ctx, "User login\nMeal planning\nBetter nutrition filters\nRestored/improved frontend interface", 650, 382, 390, 96, {
    fontSize: 21,
    color: "#33443D",
  });

  ctx.addShape(slide, { x: 82, y: 586, w: 1008, h: 2, fill: "#315348", line: ctx.line("#00000000", 0) });
  label(slide, ctx, "Questions & Answers", 82, 612, 1010, 46, { fontSize: 34, bold: true, color: C.gold, align: "center" });

  return slide;
}
