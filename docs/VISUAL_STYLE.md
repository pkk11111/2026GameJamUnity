# GROWL AGAIN Visual Style

## Reference Images

- [Reference 1](visual_refs/ref1.png)
- [Reference 2](visual_refs/ref2.png)
- [Reference 3](visual_refs/ref3.png)

## Core Direction

- Hand-drawn pencil / charcoal / crayon feeling
- Torn paper / collage texture
- Dirty grayscale as the main palette
- Off-white hand-drawn character art
- Rough black silhouettes and irregular edges
- Red/orange reserved for HP, lava, danger and important negative feedback
- Muted green may appear as minor environmental texture
- UI should feel handmade, chunky and simple rather than polished/futuristic

## Placeholder UI Rule
Current UI may use placeholders, but placeholders should roughly match the visual direction.

All visual elements must remain replaceable through Prefab / Inspector / Sprite / TMP / Animator.
Do not hard-code final colors, sprites, fonts, dimensions or asset paths in gameplay code.

## Avoid

- Glassmorphism
- Glossy gradients
- Neon sci-fi UI
- Clean futuristic panels
- Highly polished mobile-card UI
- Saturated rainbow palettes

## Usage for Development

- For tasks involving UI, Presentation, art placeholders, menus, HUD, result screens, buttons or visual feedback, read this document and view all three reference images first.
- The reference images define the visual direction; they are not final art assets.
- Placeholders should roughly follow this direction. All sprites, fonts, colors, button appearance, layouts and animations must remain replaceable through Prefab / Inspector / TMP / Animator or equivalent Unity editing tools.
- Do not hard-code final visual parameters or resource paths in C# to imitate the reference images.
- Gameplay / system-only tasks do not need additional logic changes for the visual style.
