import { C, bg, label, pill } from "./theme.mjs";

export async function slide01(presentation, ctx) {
  const slide = presentation.slides.add();
  bg(slide, ctx, 1);

  ctx.addText(slide, {
    text: "CookingRecipe",
    x: 72,
    y: 92,
    w: 720,
    h: 78,
    fontSize: 58,
    bold: true,
    color: C.cream,
    typeface: ctx.fonts.title,
  });
  ctx.addText(slide, {
    text: "Recipe recommendation system for ingredient-based meal discovery",
    x: 76,
    y: 176,
    w: 760,
    h: 66,
    fontSize: 25,
    color: "#CFE0D7",
  });
  ctx.addText(slide, {
    text: "Presented by: [Your Name]",
    x: 76,
    y: 270,
    w: 470,
    h: 38,
    fontSize: 22,
    color: C.gold,
    bold: true,
  });

  label(slide, ctx, "Project idea", 76, 365, 180, 30, { fontSize: 15, bold: true, color: C.gold });
  label(
    slide,
    ctx,
    "The system helps users find meals from ingredients they already have. It supports Nigerian recipes, live Spoonacular search, favorites, and search history.",
    76,
    400,
    680,
    102,
    { fontSize: 23, color: C.cream, insets: { left: 0, right: 10, top: 0, bottom: 0 } },
  );

  const x = 808;
  ctx.addShape(slide, { x, y: 116, w: 330, h: 392, fill: C.paper, line: ctx.line("#D8CDBA", 1) });
  label(slide, ctx, "How the idea flows", x + 30, 146, 260, 28, { fontSize: 18, bold: true });
  const steps = [
    ["Ingredients", C.green],
    ["Nigerian dataset", C.gold],
    ["Spoonacular API", C.blue],
    ["Recipe results", C.red],
  ];
  steps.forEach(([text, color], i) => {
    const yy = 206 + i * 68;
    pill(slide, ctx, text, x + 48, yy, 234, color);
    if (i < steps.length - 1) {
      label(slide, ctx, "down", x + 140, yy + 38, 60, 20, { fontSize: 11, color: "#6B7B72", align: "center" });
      ctx.addShape(slide, { x: x + 162, y: yy + 58, w: 16, h: 2, fill: "#6B7B72", line: ctx.line("#00000000", 0) });
    }
  });

  return slide;
}
