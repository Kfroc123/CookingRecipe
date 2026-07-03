import { C, bg, title, panel, label } from "./theme.mjs";

export async function slide04(presentation, ctx) {
  const slide = presentation.slides.add();
  bg(slide, ctx, 4);
  title(slide, ctx, "System overview", "The backend checks Nigerian recipes before foreign results.");

  const nodes = [
    ["User enters ingredients", "rice, tomato, onion", 84, 270, C.green],
    ["ASP.NET Core API", "receives request", 330, 270, C.blue],
    ["Nigerian dataset first", "local priority search", 576, 230, C.gold],
    ["Spoonacular fallback", "external recipe search", 576, 360, C.red],
    ["JSON recipe response", "cards, details, favorites", 858, 270, C.green],
  ];

  nodes.forEach(([head, sub, x, y, color]) => {
    panel(slide, ctx, x, y, 210, 92);
    ctx.addShape(slide, { x: x, y, w: 8, h: 92, fill: color, line: ctx.line("#00000000", 0) });
    label(slide, ctx, head, x + 20, y + 15, 166, 28, { fontSize: 18, bold: true });
    label(slide, ctx, sub, x + 20, y + 49, 166, 24, { fontSize: 15, color: "#51635B" });
  });

  const arrows = [
    [294, 315, 326],
    [540, 315, 572],
    [786, 275, 854],
    [786, 405, 854],
  ];
  arrows.forEach(([x1, y, x2]) => {
    ctx.addShape(slide, { x: x1, y, w: x2 - x1, h: 3, fill: "#D7E8DE", line: ctx.line("#00000000", 0) });
    ctx.addShape(slide, { x: x2 - 8, y: y - 5, w: 12, h: 12, fill: "#D7E8DE", line: ctx.line("#00000000", 0) });
  });
  ctx.addShape(slide, { x: 676, y: 322, w: 3, h: 38, fill: "#D7E8DE", line: ctx.line("#00000000", 0) });

  label(
    slide,
    ctx,
    "Main users: students, families, and home cooks who want fast meal ideas from available ingredients.",
    140,
    560,
    980,
    42,
    { fontSize: 23, bold: true, color: C.cream, align: "center" },
  );

  return slide;
}
