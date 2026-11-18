# The bookshelf model: brief for Blender

What the reader app needs from Blender to replace the drawn bookcase on the shelf screen with a rendered one. Mood: a child's bedroom in London in the late 1800s. A fitted cherry nursery bookcase, warm and a little worn, against faded wallpaper. Soft and warm rather than crisp and photographic, so it sits well with watercolour covers.

## Why it comes in pieces

The app doesn't show one fixed picture. The bookcase changes shape all the time:

- **Width:** it gets wider or narrower to fit the window (a wide PC, a laptop, a phone held sideways).
- **Shelves:** it shows one, two or more shelves, depending on how many books there are and how tall the screen is.
- **Covers:** the app places the real book covers itself, so it can lift them on hover and add or remove books.

So Blender renders a kit of parts that the app stacks and repeats. Every part has to line up with the parts beside it and repeat with no visible seam.

## Model size (real-world centimetres)

Use 1 Blender unit = 1 cm. A book cover is the measuring stick.

| Part | Size |
|---|---|
| Book cover (stand-in only, not rendered) | 20 wide × 30 tall, 2:3 |
| One book slot (one book plus the space around it) | 25 wide |
| Clear height above a shelf | 35 |
| Shelf plank | 22 deep, front edge 2.8 thick |
| Case sides | 3.5 wide as seen from the front, 26 deep |
| Cornice (top) | about 8 tall, standing about 1.5 proud of the sides |
| Plinth (base) | about 6 tall, on two turned bun feet about 2.5 tall at the outer corners |
| Back | solid cherry with raised panels, recessed about 24 behind the shelf front |

Make the back panels **25 wide, exactly one panel per book slot**, so each book stands in front of its own panel. The panel frames (stiles and rails) should be about 3 wide, with a slightly lighter raised field in the middle and a soft bevel.

## Camera and light

- **Camera:** orthographic, looking straight at the front, tilted down about 6 to 8 degrees so the top of each shelf shows a little. Orthographic is essential: with perspective, repeated pieces won't line up.
- **Scale:** 30 pixels per cm for the masters (a 30 cm book is 900 px tall). The app shrinks them as needed.
- **Light:** a warm key light from the upper left, like a window, plus a soft fill. Use the same light for every part, or the pieces won't match when stacked. The app's cover shadows fall down and slightly right to match.
- **Colour:** render in sRGB. Use Blender's **Standard** view transform, or AgX with care, so the cherry comes out as the colour you see and doesn't fade. Aim for a warm reddish brown: roughly `#9a4a2c` in the midtones, `#5e2a18` in the shadows.

## Materials

- **Cherry wood:** a real wood texture (Poly Haven and ambientCG have free scanned woods; tint to cherry). Satin finish with soft, low reflections. Slightly darker in the corners and recesses (ambient occlusion), and a little wear on the front edges of the shelves and the cornice.
- **Brass:** soft, slightly tarnished, not mirror bright. Use it on the small label plate in the cornice and, optionally, thin strips on the shelf fronts.
- **Wallpaper:** faded Victorian nursery paper, small sage sprigs or stripes on cream, low contrast. A public-domain scan of a real 1880s paper works well. It must tile seamlessly.
- **Dado rail and panelling below:** painted wood in a soft sage or cream, with a cherry or painted rail.

## The parts to render

All PNG, 8-bit sRGB. Parts marked "alpha" need a transparent background with their own soft shadow baked in. Render the wall parts on their own with no bookcase in front.

**The case**

1. `cornice_left.png`, `cornice_mid.png`, `cornice_right.png`: the top. The middle piece is exactly one slot (25 cm) wide and repeats sideways. Alpha above the cornice.
2. `cornice_plate.png`: the brass label plate on its own (alpha). The app centres it.
3. `side_left.png`, `side_right.png`: the case sides for one shelf height (35 cm clear plus the plank). They repeat downwards, one per shelf.
4. `bay_back.png`: the back for one slot and one shelf height, a single raised panel. It repeats in both directions. Include the light falling on it, but no books.
5. `shelf_front.png`: the plank for one slot width, showing its top surface going back and its front edge. Repeats sideways.
6. `shelf_shadow.png`: the soft shadow the shelf above casts onto the back and onto the shelf below (alpha, multiply). It goes over `bay_back`.
7. `plinth_left.png`, `plinth_mid.png`, `plinth_right.png`: the base. The feet are on the left and right pieces; the middle repeats. Alpha below.
8. `corner_inner_*.png` (optional): the shadow where the sides meet the back, if it can't be baked into the side pieces.

**The wall**

9. `wallpaper_tile.png`: one seamless repeat of the paper, lit evenly. The app adds the window light over it.
10. `dado.png`: the rail plus the panelling below for one repeat width, seamless sideways.

**Props** (alpha, each with its contact shadow)

11. `fern.png`: a Boston fern in a glazed jardinière, about one slot wide.
12. `books_pile.png`: two to four clothbound books lying flat (bottle green, oxblood, navy, gilt bands).
13. Optional, only if it looks good and not cluttered: one period toy (a wooden spinning top or a small tin soldier).

**For checking only**

14. `preview_full.png`: the whole bookcase put together, three slots wide and two shelves high, with plain stand-in covers in place, so we can judge the look before I build it into the app.

## Rules that make the pieces fit

- Every repeating piece must match itself exactly at its edges (the wood grain too). Check by putting two copies side by side.
- Render every piece with the same camera, the same lights and the same scale. The simplest way is one scene: render each part by hiding everything else and setting the render border to that part.
- Keep the edges of each piece on exact pixel lines: at 30 px per cm, a 25 cm slot is exactly 750 px.
- No books in any render apart from the preview. The app draws them.

## What happens after

Once the parts exist, I change the app's bookcase to stack them instead of drawing it. The app also needs a phone version of the layout, but it uses the same parts scaled down, so nothing extra has to be rendered. The rendered parts add an estimated 2 to 5 MB to the app. They're loaded once, so they cost nothing in smoothness.

Doing it by script: Blender can be driven entirely from Python (`blender -b scene.blend --python build.py`). A Claude session running on your PC can build this scene, render every part to the sizes above and check the seams, while you open the same `.blend` file and adjust the wood and lighting by eye.
