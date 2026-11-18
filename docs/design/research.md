# Research for the reader's design

Date: November 2026. Each finding has its source. Where I only reached a summary of a study and not the study itself, the line says "(secondary)". Weak or conflicting evidence is flagged in place and collected again in section 11.

Unit note: 1 dp is 1/160 inch, so 1 cm is 63 dp and 2 cm is 126 dp. On the phone mockups 1 px is 1 dp.

## 1. How children aged 3 to 6 use a touch screen (and how 6 to 9 differ)

**Hands**

- Children under 5 have limited motor skill and need very simple touch actions. Tapping big targets, swiping and loose dragging work from age 3; precise dragging does not (a 5-year-old could not place objects exactly). https://www.nngroup.com/articles/children-ux-physical-development/
- Recommended touch target for young children is at least 2 cm by 2 cm, four times the area of the 1 cm adult target. A 5 mm close button defeated two 7-year-olds. Same source.
- In a lab study children missed touch targets 46% of the time against 32% for adults, across mixed sizes and positions. https://lisa-anthony.com/wp-content/uploads/2012/03/brown-and-anthony-chi2012eist.pdf
- Avoid two-handed actions and quick actions for under 5s, and keep them limited up to 8. Controls close together cause accidental triggers (a 5-year-old changed the view by brushing a neighbouring control). https://www.nngroup.com/articles/children-ux-physical-development/
- Sesame Workshop's guidance from 50+ studies with preschoolers: tap is the most natural gesture; pinch and flick are hard; swipe works if the screen shows where to swipe; partly completed traces should count. (secondary) https://joanganzcooneycenter.org/?p=19607 and https://www.theregister.co.uk/2012/12/20/sesame_street_touch_apps_best_practice/
- Preschoolers hold a tablet sideways and rest wrists or palms on the bottom edge, so hot spots along the bottom edge fire by accident. (secondary) https://appleinsider.com/articles/12/12/24/sesame-workshop-wants-to-improve-ipad-apps-for-kids-with-free-developer-guidelines
- Register a tap when the finger lands, not when it lifts, and confirm every touch with a sound. (secondary) https://www.theregister.co.uk/2012/12/20/sesame_street_touch_apps_best_practice/
- From 6 to 8 simple clicks and keys work; from about 9 more advanced actions; at about 11 children match adults. https://www.nngroup.com/articles/children-ux-physical-development/

**Heads**

- Children aged 3 to 5 mostly cannot read, think in pictures, and learn an interface by repetition and muscle memory, so controls must stay in the same place and be shown as pictures with spoken support. (secondary summary of NN/g's cognition work) https://uxmatters.com/mt/archives/2020/01/ux-design-for-kids-key-design-considerations.php
- Use familiar, standard patterns and not invented ones. Same source.
- Fewer screens is better: 3 to 5-year-olds readily used "more like this" suggestions that saved them navigating. https://www.nngroup.com/articles/children-ux-physical-development/
- Children cannot tell adverts and promotions from content, and an accidental tap on one takes them out of the app. https://www.nngroup.com/articles/children-ux-physical-development/
- Conflict to note: NN/g found dragging easy on touch for the youngest, but precise dragging failed. Treat drag as "pan around", never "drop exactly here".
- Conflict to note: the "keep hot spots off the bottom edge" advice comes from tablets held flat or on a lap. On a phone held sideways in two hands, thumbs sit at the lower corners. There is no study I could find that settles this for phones.

**Exits and gates**

- Apple's guidance for children's apps: anything that leaves the app or involves buying must sit behind a gate a young child cannot pass, such as a sum or a question that needs reading, and the gate must not be switchable off. https://developer.apple.com/app-store/parental-gates/
- Not found: a controlled study comparing gate types by age. The choice of gate below is reasoned from reading age, not measured.

## 2. What digital picture books do to understanding

- Meta-analysis of 43 studies, 2,147 children: animation, music and sound that fit the story gave a small gain in story understanding (g = 0.17) and vocabulary (g = 0.20). Interactive extras such as hot spots and games did not help and hurt children from less stimulating homes most. https://docs.opendeved.net/lib/NDJ8IRSS
- A companion meta-analysis found story-matched animation, music and sound effects supported understanding about as well as an adult's support did. https://www.frontiersin.org/articles/10.3389/fpsyg.2014.01366/text
- Parent and child pairs (32, ages 3 to 6): children recalled fewer story details from an e-book loaded with extras than from print; a plain e-book did as well as print. The authors advise letting parents switch features. Small study. https://joanganzcooneycenter.org/?p=6824
- Four things make an app teach: the child is mentally active (tapping alone does not count), engaged and not distracted, the content is meaningful, and there is another person in the loop. https://www.psychologicalscience.org/publications/educational-apps.html
- An on-screen character who modelled questions made parents talk three times more while reading, and parents kept asking questions after the character was gone. 32 pairs per group. https://news.vanderbilt.edu/2020/06/29/enhanced-e-book-helps-close-the-word-gap-in-children-from-families-of-low-socio-economic-status
- Word highlighting in time with the voice: pre-K children looked at pictures 82% of the time and print 16%; about half their looks at print matched the highlighted word; only 2 of 8 gained on word recognition in one sitting. Tiny study. https://informahealthcare.com/doi/abs/10.1080/15391523.2021.1938763
- A review for early-years teachers reports e-books with highlighted narration improving print awareness in preschoolers, and that children learned most when an adult read along. https://www.naeyc.org/resources/pubs/yc/may2016/ebooks-for-children
- Evidence is thin here: highlighting plausibly links sound to print, but nobody has shown strong gains for 3 to 6-year-olds. Keep it, do not sell it as a reading method.

What this means for the product: the moving camera, the weather and the story sounds are on the right side of this evidence, because they point at the story. Find-it pages are on the risky side unless they are part of the story (as the lost ducklings are) and come after it, not during it.

## 3. Attention, stimulation and reward

- Kindergarten children in a heavily decorated room spent 38.6% of the time off task against 28.4% in a plain room, and learned less. Lab classroom, small sample. https://www.cmu.edu/news/stories/archives/2014/may/may27_decoratedclassrooms.html
- Nine minutes of a fast-paced cartoon lowered 4-year-olds' self-control scores straight afterwards, compared with a slow programme or drawing. 60 children, immediate effect only. https://www.sciencedaily.com/releases/2011/09/110912075658.htm
- In a study of 160 children aged 3 to 5, nearly all had at least one top app using manipulative design: characters pressuring the child to keep going, made-up time pressure, lures, navigation traps, advert pressure. Children from poorer homes met more of it. https://michiganmedicine.org/health-lab/design-tricks-commonly-used-monetize-young-childrens-app-use
- Meta-analysis of 128 experiments: tangible rewards given for doing an activity reduce later free-choice interest in it, and the effect is stronger in children. https://home.ubalt.edu/ntygmitc/642/Articles%20syllabus/Deci%20Koestner%20Ryan%20meta%20IM%20psy%20bull%2099.pdf
- For reading in particular, a reward that is itself reading (a book, more reading time) did not harm motivation, where a token did. (secondary) https://www.readingrockets.org/topics/motivation/articles/simple-practices-nurture-motivation-read
- Disputed: Cameron and Pierce argue the undermining effect is small in schools. The reading-specific evidence is one main study. The safe reading of it is the same either way: do not hand out points or prizes for reading.
- A large children's reading app that uses badges, levels and streaks draws the complaint that children click through books to collect badges. Parent-guide sources, not studies. https://www.internetmatters.org/advice/apps-and-platforms/skills-building/epic/

## 4. Colour

- Reviews say young children prefer vivid, bright colours, but a 2023 eye-tracking study of 4 to 7-year-olds found them drawn more as saturation fell and brightness rose within a range. The authors call the field inconclusive. https://www.frontiersin.org/journals/psychology/articles/10.3389/fpsyg.2023.1205213/pdf
- Strong brightness contrast is what grabs attention, wanted or not, and the youngest are worst at ignoring it. Same source's review section.
- Colour judgement of a picture shifts with the colour around it; the print trade views artwork against a neutral, matt surround for that reason. Vendor and trade sources, not studies of children. https://piworld.com/article/key-accurate-color-viewing
- Text contrast: 4.5:1 for normal text, 3:1 for large text (24 px, or 19 px bold). Controls and meaningful graphics need 3:1 against what is next to them. https://webaim.org/articles/contrast/
- Colour vision deficiency: about 8% of men and 0.4% of women of European descent, 4 to 6.5% of men in East Asian studies; a 2025 pooled figure is 4.4% of males. https://pubmed.ncbi.nlm.nih.gov/22472762/ A Durban study of Black schoolchildren found lower rates. https://www.ncbi.nlm.nih.gov/pmc/articles/PMC6558866/
- Not found: good evidence on colour and culture for South African families. "Culturally neutral" below means only: avoid colours and motifs that belong to one group, flag or era.
- Weak overall. No study tells us which palette "works" for a picture-book shelf. What the evidence supports is narrower: a light, quiet, low-contrast surround so the covers carry the colour; one or two strong accents on the things to press; never colour alone to carry meaning.

## 5. Type for early readers

- Six-year-olds read equally well in serif and sans faces, with or without "infant" single-storey a and g. They do notice the difference and have views. https://centaur.reading.ac.uk/25234/ (abstract; performance result via secondary summaries)
- Beginning readers need larger type than adults; 14 pt print is the usual floor cited. (secondary) https://www.uxmatters.com/mt/archives/2011/06/effective-use-of-typography-in-applications-for-children-3.php
- Bigger is not always better: enlarging type past a normal size reduced word-reading accuracy in beginners. https://escholarship.org/uc/item/5m91913s
- Word and letter spacing matter to children ("you can't see what the words say"). https://benjamins.com/catalog/swll.17.02wal (abstract only)
- 94 children from kindergarten to grade 8: no one font was best; the best font differed by child. https://mdpi.com/2227-7102/13/9/864
- Lexend's claim of better reading rests on its maker's own small study; type designers dispute it. https://typedrawers.com/discussion/comment/43214
- Andika (SIL, Open Font License) was drawn for beginning readers with letters that are hard to confuse. Design rationale, no outcome trial found. https://ctan.net/fonts/andika/README
- Atkinson Hyperlegible (Open Font License) separates look-alike characters for low-vision readers; the Next version has seven weights and 150+ languages. No reading trial found. https://github.com/googlefonts/atkinson-hyperlegible
- Afrikaans needs ê ë é è ô û î ï á ú ó and the ʼn article. Checked locally: the Nunito files built into the app report full Afrikaans coverage (fontconfig lists `af`). Fredoka, Fraunces and Andika from Google Fonts cover Latin Extended too.
- Looked at directly: Nunito has a single-storey g and a two-storey a; its capital I and lowercase l are near twins. Andika and Atkinson tell I, l and 1 apart.

## 6. What other children's reading apps do

Checked from sources:

- **African Storybook Reader**: 1,000+ free picture books in African languages, downloaded into an offline library, sorted into 5 reading levels. https://help.africanstorybook.org/documents/howto/Use_the_Reader_App.pdf
- **Book Dash**: 200+ free South African picture books in 11 languages, download to read later, 100,000+ installs. https://bookdash.org/free-reading-platforms/
- **Khan Academy Kids**: five animal characters guide the child; a Library with read-aloud books; one part of the library works offline; a parent section can switch content areas off. https://khankids.zendesk.com/hc/en-us/articles/360006764812
- **Epic**: child profiles, badges, levels, collectable buddies, streaks; criticised for badge-chasing (section 3).
- **Vooks**: animated storybooks with read-along highlighting, sold on that feature. Vendor page. https://info.vooks.com/word-highlighting-english-learners-research
- **Pok Pok**: deliberately quiet, no points, coins, timers or levels; reviewers say children stop by themselves when done. Reviews, not the maker's own statement. https://screenwiseapp.com/guides/pok-pok-playroom-app

From general knowledge of these products, not re-checked for this report (Sago Mini, Toca Boca, Lingokids, Libby and Sora, Skybrary, CBeebies Storytime): a "who is playing" picker with picture avatars; covers shown face-out in rows; a "keep reading" row first; a parents' corner behind a gate in a top corner; a named animal mascot; white or pale backgrounds for premium quiet apps and saturated full-colour worlds for mass-market ones.

Patterns worth copying: face-out covers, a resume row, picture avatars, a gated parents' corner, offline first, a "what next" suggestion at the end. Patterns to avoid are in section 3.

Gap in the market, as far as this search shows: the South African apps are free libraries of flat pages. None found offers narrated, moving picture books in Afrikaans.

## 7. Parents, trust, privacy and the South African setting

- About three in four parents of under-8s worry about screens and the same share value what children learn from them. US survey. https://www.commonsensemedia.org/research/the-2025-common-sense-census-media-use-by-kids-zero-to-eight
- Most parents have downloaded children's apps but only a quarter have paid for one; "free" usually means adverts or data. https://www.commonsensemedia.org/kids-action/articles/new-research-raises-privacy-concerns-for-young-kids-and-families-media-use
- Google Play Families policy: an accurate privacy policy, no personalised adverts, only self-certified advert kits, disclose anything collected from children, no device identifiers sent on. https://support.google.com/googleplay/android-developer/answer/9893335
- Teacher Approved is a separate quality review that looks at adverts, purchases, cross-promotion, language and audio, and visual quality for the age; badged apps appear in the Kids tab. https://play.google.com/console/about/programs/teacherapproved/ A 2026 study questions how much the badge proves. https://educationhq.com/news/googles-teacher-approved-badge-on-kids-apps-is-problematic-study-209535/
- POPIA forbids processing a child's personal information unless an exception applies, the main one being consent from a competent person (parent or guardian). There is no lower "digital age"; a child is anyone under 18. https://inforegulator.org.za/knowledge-base/category/popia/chapter-3-conditions-for-lawful-processing/part-c-processing-of-personal-information-of-children/ Not legal advice.
- Android has 76% of South African phones. https://gs.statcounter.com/os-market-share/mobile/south-africa
- Data is dear: headline 1 GB bundles from the big networks sit around R149 to R160, with averages across all plans far lower (R20 to R37). Sources differ by method. https://techcentral.co.za/sa-users-spurning-cheaper-data-deals/198361/ and https://scrolla.africa/sa-pays-more-for-data-than-27-other-african-countries-new-report-finds/
- Cheap phones dominate Africa: under-$100 phones were 30 to 45% of shipments, and an entry phone can cost 73% of a low-income adult's monthly income. Whole-continent figures, not South Africa alone. https://omdia.tech.informa.com/pr/2026/aug/africa-smartphone-market-expected-to-decline-26percent-in-2026-following-first-contraction-in-three-years
- 81% of South African grade 4 children cannot read for meaning in any language (2021). English and Afrikaans schools held steady from 2016; others fell. https://nicspaull.com/2023/05/18/10-main-findings-from-pirls-2021-south-africa
- Afrikaans is the home language of 10.6% of South Africans, about 6.6 million people. https://en.wikipedia.org/wiki/2022_South_African_census
- Not found: a survey of what South African parents look for in a children's app, or typical memory in phones sold there. The design assumes 2 to 3 GB phones with small screens; test on one.

The app as built already asks for no permissions, has no internet access, no accounts and no links out. That is rarer than it should be and is the strongest thing to tell parents.

## 8. Motion, sound and bedtime

- Story-matched animation and sound help (section 2); fast pace costs self-control straight after (section 3).
- Sound that confirms a touch helps preschoolers know it worked (section 1).
- WCAG 2.3.3 asks that motion triggered by interaction can be switched off. The app's Movement setting already does this and follows the device. (W3C page not re-read for this report.) https://www.w3.org/WAI/WCAG22/Understanding/animation-from-interactions.html
- Bedtime device use goes with too little sleep across 125,000 children (odds ratio about 2). Observational. https://pmc.ncbi.nlm.nih.gov/articles/PMC5380441
- Preschoolers' melatonin is strongly suppressed by evening light, even at low levels; bright light cut it by about 88% for at least 50 minutes after. Small samples. https://www.sciencedaily.com/releases/2022/01/220127104208.htm
- A 2026 within-child review found screen days mean later bedtimes but little change in total sleep. (secondary) https://press-news.org/221101-screen-time-delays-when-kids-go-to-sleep-but-does-not-shrink-how-long-they-sleep.html
- Not found: evidence that a warm or dark colour scheme in an app protects a child's sleep. A dimmer screen emits less light, which is the only claim to make.

## 9. Characters and mascots

- Toddlers and preschoolers learn more from a character they know and feel close to; trust in the character is what carries learning over to real life. (secondary summary of Calvert's work) https://www.childrenandscreens.org/learn-explore/research/young-children-media-characters-and-learning/
- The same bond is what manipulative apps exploit, with characters who plead or scold (section 3).
- No direct evidence on brand mascots in book apps.

## 10. Tools for the people who make the books

Evidence here is trade practice, not research.

- Picture editors use a neutral grey frame because a coloured or very dark frame shifts how the artwork's colour and brightness are judged; a very dark frame tends to make people darken their pictures. https://community.adobe.com/t5/photoshop-ecosystem-discussions/why-is-the-background-color-of-the-photoshop-application-designed-gray/m-p/10078168 and https://photography.tutsplus.com/tutorials/want-better-color-use-neutral-gray-themes-in-adobe-photoshop-and-lightroom--cms-23342
- Photoshop has a mode that turns even its accent-coloured buttons grey so nothing competes with the picture. https://www.photoshopessentials.com/?p=33232
- Not searched to a source: progressive disclosure, tooltips with shortcuts, undo everywhere, visible save state. These are standard practice and the editor list in the proposal leans on them as such.

## 11. Where the evidence is weak or disputed

1. Target size on phones. The 2 cm rule is from tablets and one usability firm's testing. No phone study for ages 3 to 6 was found.
2. Bottom-edge hot spots. Tablet finding; may not hold for a phone held in two hands.
3. Colour preference. Mixed, and nothing on South African children.
4. Fonts. No font is shown to be better for early readers. Size and spacing matter more than face.
5. Word highlighting. Plausible, lightly supported for under 6s.
6. Rewards. The lab evidence is strong; the reading-specific evidence is thin and disputed. Avoiding prizes costs nothing.
7. Bedtime colours. No direct evidence.
8. Mascots in apps. Inferred from television research.
9. Debra Gelman's "Design for Kids" and the full Sesame Workshop PDF were not read directly; their points appear only where a summary could be cited.
10. Several app descriptions come from review sites, not the makers.

## 12. What this means for us: the rules

**Touch**

1. Anything a child presses is at least 56 dp across. The main action on each screen (a cover, Next, Carry on) is at least 72 dp and is the biggest thing after the artwork. Aim for 80 dp or more where there is room; 126 dp (2 cm) on tablets.
2. Grown-up-only controls are 48 dp and visibly smaller and quieter than the child's.
3. At least 16 dp of clear space between any two child targets, and 24 dp between Next and anything else.
4. Child gestures are tap, and loose drag to look around. Nothing needs pinch, double tap, long press, precise drag or two hands. Every pinch has a button that does the same.
5. A tap counts when the finger lands. Every tap is answered within 100 ms by movement and a soft sound.
6. No control within 16 dp of the bottom edge. On tablets, keep child controls out of the bottom 48 dp in the middle half of the screen where wrists rest.
7. Controls never move. Back to books is always top left, Next always lower right, Back always lower left.
8. A wrong tap never costs anything: no "wrong" sound, no lost place, and leaving a book keeps the page.
9. Leaving a book takes one tap and can be undone in one tap (Carry on). No "are you sure" boxes for children.

**Navigation**

10. A child is never more than one tap from a book and two taps from reading: picker, shelf, book. No menus, tabs or scrolling lists on the child's side for a shelf of up to 8 books; one vertical scroll after that.
11. Every child-facing label has a picture and works without reading. Labels are 1 to 3 words.
12. Nothing on the child's side leaves the app or leads to settings. All of that sits behind one gate.
13. The gate asks the grown-up to tap three numbers written as words, in the app's language, new each time, with a 30 second pause after three misses. It cannot be turned off.

**Calm**

14. The shelf has one moving thing at most at any time, and nothing moves when nobody has touched it for 2 seconds.
15. No points, coins, stars-for-reading, streaks, levels, timers, countdowns or "come back" notices. Progress is shown as facts: a bookmark, a tick, pages read.
16. The mascot never asks the child to stay, never looks sad, and is silent during a story.
17. Interface animations last 150 to 300 ms; the page turn stays at 900 ms. Reduced motion replaces all of them with a fade.
18. Sounds are short (under 400 ms), soft, and pitched low to middle. One volume step below the story voice. A single switch turns interface sounds off; story sounds follow Read to me.
19. Find-it pages come after the story or are part of it. They never interrupt a sentence.

**Colour**

20. The surround is light, warm and low in saturation, so covers and pages are the most colourful things on screen. No pattern or texture behind covers.
21. At most two strong accent colours in the interface, used only on things to press and on state.
22. Body text 4.5:1 or better; large text and control outlines 3:1 or better. The proposal lists the measured pairs.
23. State is never shown by colour alone: finished is a tick, in progress is a bookmark shape, the word being read gets a filled box and not only a tint.
24. No red and green pair as the only difference between two things.
25. During reading the interface adds no colour: controls are cream or dark glass, plus one accent for Next.

**Type**

26. Words children read are at least 22 sp on phones and 28 sp on tablets and Windows, in a rounded sans with open shapes, weight 700 to 800, line spacing 1.3, no more than about 45 characters a line, never all capitals except where the story shouts.
27. Do not go above about 34 sp for running story text on a phone; past that, accuracy does not improve.
28. Grown-up text is at least 14 sp; labels 16 sp.
29. Keep Nunito for story words (books are already laid out in it and the evidence gives no reason to switch). Offer Andika as an "easier letters" option later, as a per-child setting, because the best face differs by child.
30. Every font must cover ê ë é è ô û î ï á ú ó and ʼn. Test strings: "Sê vir my", "reën", "môre", "ʼn brûe".

**Trust**

31. First-run and the store page say in plain words: no adverts, no sign-up, nothing collected, works offline. These must stay true.
32. A child's profile is a first name or nickname and a picture, stored only on the device. No birthday, no photo, no account.
33. Show parents facts about reading (books finished, days read), never a score.
34. Total app download under 60 MB; each book file shows its size before it is added.

**Bedtime**

35. Offer darker, dimmer "bedtime colours" as a parent switch, described only as "darker and quieter", with no health claim.
