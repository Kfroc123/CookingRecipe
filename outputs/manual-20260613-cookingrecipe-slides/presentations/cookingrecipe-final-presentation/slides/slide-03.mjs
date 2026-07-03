import { C, bg, title, panel, label } from "./theme.mjs";

export async function slide03(presentation, ctx) {
  const slide = presentation.slides.add();
  bg(slide, ctx, 3);
  title(slide, ctx, "Project objectives", "The system is designed around five clear backend goals.");

  const objectives = [
    ["01", "Search recipes using ingredients"],
    ["02", "Prioritize Nigerian recipes from a local dataset"],
    ["03", "Fetch more recipes from the Spoonacular API"],
    ["04", "Allow users to save favorite recipes"],
    ["05", "Store and view search history"],
  ];

  objectives.forEach(([num, text], i) => {
    const y = 210 + i * 75;
    ctx.addShape(slide, { x: 112, y: y + 18, w: 950, h: 2, fill: "#315348", line: ctx.line("#00000000", 0) });
    ctx.addShape(slide, { x: 112, y, w: 58, h: 58, fill: i === 1 ? C.gold : C.green, line: ctx.line("#00000000", 0) });
    label(slide, ctx, num, 112, y + 13, 58, 28, { fontSize: 20, bold: true, color: "#FFFFFF", align: "center", valign: "mid" });
    panel(slide, ctx, 196, y - 4, 760, 66, i === 1 ? "#FFF2C6" : C.paper);
    label(slide, ctx, text, 218, y + 14, 700, 34, { fontSize: 25, bold: i === 1, color: C.darkText });
  });

  label(slide, ctx, "Presentation focus", 1010, 238, 150, 24, { fontSize: 14, bold: true, color: C.gold });
  label(
    slide,
    ctx,
    "Show that the project works, explain the logic, and connect each feature back to the problem.",
    1010,
    272,
    160,
    160,
    { fontSize: 20, color: C.cream, insets: { left: 0, right: 0, top: 0, bottom: 0 } },
  );

  return slide;
}
