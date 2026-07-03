import { C, bg, title, panel, label } from "./theme.mjs";

export async function slide05(presentation, ctx) {
  const slide = presentation.slides.add();
  bg(slide, ctx, 5);
  title(slide, ctx, "Technology used", "The stack separates interface, API logic, storage, and external data.");

  const rows = [
    ["Frontend", "React + Vite", "Displays search, recipe details, favorites, and history.", C.green],
    ["Backend", "ASP.NET Core Web API / .NET 9", "Handles endpoints, business logic, CORS, health checks, and Swagger.", C.blue],
    ["Database / storage", "SQLite + optional Redis", "SQLite supports app data; Redis stores favorites/search history with in-memory fallback.", C.gold],
    ["External tools", "Spoonacular API, Swagger, Docker/Render", "Adds live recipe data, API testing, and deployment support.", C.red],
  ];

  rows.forEach(([area, stack, note, color], i) => {
    const y = 205 + i * 100;
    ctx.addShape(slide, { x: 86, y, w: 112, h: 58, fill: color, line: ctx.line("#00000000", 0) });
    label(slide, ctx, area, 92, y + 17, 100, 22, { fontSize: 15, bold: true, color: "#FFFFFF", align: "center", valign: "mid" });
    panel(slide, ctx, 222, y - 12, 880, 82);
    label(slide, ctx, stack, 246, y + 1, 370, 28, { fontSize: 23, bold: true });
    label(slide, ctx, note, 246, y + 38, 820, 24, { fontSize: 17, color: "#4B5D55" });
  });

  label(slide, ctx, "Key point: the frontend calls the backend API, and the backend returns recipe data as JSON.", 130, 624, 990, 32, {
    fontSize: 21,
    color: C.cream,
    align: "center",
  });

  return slide;
}
