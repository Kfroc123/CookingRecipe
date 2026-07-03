import { C, bg, title, panel, label } from "./theme.mjs";

export async function slide06(presentation, ctx) {
  const slide = presentation.slides.add();
  bg(slide, ctx, 6);
  title(slide, ctx, "Live demonstration", "The demo should prove the main API features in order.");

  panel(slide, ctx, 78, 208, 520, 360);
  label(slide, ctx, "Open Swagger", 112, 240, 230, 30, { fontSize: 24, bold: true });
  label(slide, ctx, "http://localhost:5209/swagger", 112, 292, 410, 34, {
    fontSize: 20,
    color: C.green,
    typeface: ctx.fonts.mono,
  });
  label(
    slide,
    ctx,
    "Explain while testing: the backend accepts ingredients, searches local Nigerian recipes first, then uses Spoonacular when needed.",
    112,
    365,
    410,
    112,
    { fontSize: 22, color: "#30433B" },
  );

  const endpoints = [
    "GET /health",
    "GET /api/recipes",
    "GET /api/recipes/search?ingredients=rice,tomato&max=10",
    "GET /api/recipes/{id}",
    "POST /api/recipes/{id}/favorite",
    "GET /api/recipes/favorites",
    "GET /api/searchhistory",
  ];
  panel(slide, ctx, 640, 190, 500, 410, "#18362D", "#315348");
  label(slide, ctx, "Demo checklist", 680, 222, 220, 28, { fontSize: 24, bold: true, color: C.cream });
  endpoints.forEach((endpoint, i) => {
    const y = 272 + i * 42;
    ctx.addShape(slide, { x: 680, y: y + 7, w: 14, h: 14, fill: i === 0 ? C.green : C.gold, line: ctx.line("#00000000", 0) });
    label(slide, ctx, endpoint, 708, y, 390, 28, {
      fontSize: endpoint.length > 42 ? 16 : 18,
      color: "#EAF3EE",
      typeface: ctx.fonts.mono,
    });
  });

  return slide;
}
