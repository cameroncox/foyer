# Docs

Design documents written while planning Foyer, exported from the Claude artifacts they were
drafted in. They describe the plan as of v1.0.0; where the code and these differ, the code wins.

| File | What |
| --- | --- |
| [spec.md](spec.md) | Foyer Spec: behaviour, labels, data model, UI, API sketch, out of scope |
| [project-layout-plan.md](project-layout-plan.md) | Project Layout Plan: repo tree, stack choices, tests, CI, build order |
| [wireframes/](wireframes/) | Foyer Wireframes: 14 desktop and phone artboards |

The spec's architecture diagram was an embedded widget and didn't survive the Markdown export;
it shows as a placeholder line.

The wireframes are the canvas's source files: one `.dc.html` per artboard, with `canvas.json`
giving each artboard's title, size and position. They need the design canvas's runtime
(`support.js`) to render, so they won't display opened directly in a browser; read them as
markup, or view them in the original canvas.
