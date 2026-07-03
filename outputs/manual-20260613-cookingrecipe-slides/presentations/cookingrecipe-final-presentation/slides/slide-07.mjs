import { C, bg, title, panel, label } from "./theme.mjs";

export async function slide07(presentation, ctx) {
  const slide = presentation.slides.add();
  bg(slide, ctx, 7);
  title(slide, ctx, "Challenges", "The project needed fallbacks for real-world backend issues.");

  const rows = [
    ["External API", "Connected Spoonacular and handled provider errors or quota limits."],
    ["Missing secrets", "Kept API keys out of code and used configuration/environment variables."],
    ["Local priority", "Searched the Nigerian dataset before foreign recipe results."],
    ["CORS", "Allowed the frontend origin so browser requests can reach the backend."],
    ["Redis availability", "Made Redis optional by adding an in-memory fallback."],
  ];

  panel(slide, ctx, 90, 205, 1050, 370);
  label(slide, ctx, "Challenge", 126, 232, 250, 24, { fontSize: 18, bold: true, color: C.green });
  label(slide, ctx, "How I handled it", 420, 232, 520, 24, { fontSize: 18, bold: true, color: C.green });
  rows.forEach(([challenge, response], i) => {
    const y = 282 + i * 58;
    ctx.addShape(slide, { x: 116, y: y - 12, w: 990, h: 1, fill: "#D8CDBA", line: ctx.line("#00000000", 0) });
    label(slide, ctx, challenge, 126, y, 230, 28, { fontSize: 21, bold: true, color: i === 0 ? C.red : C.darkText });
    label(slide, ctx, response, 420, y, 645, 34, { fontSize: 20, color: "#33443D" });
  });

  label(slide, ctx, "This is important because deployed apps must keep working even when services or config are imperfect.", 120, 620, 1000, 30, {
    fontSize: 20,
    color: C.cream,
    align: "center",
  });

  return slide;
}
