namespace Reader.Core;

/// <summary>
/// Every word the app itself shows, in each language it speaks. One table: a key, then the words in
/// the order of <see cref="Languages"/>. To add a language, add its code and name to
/// <see cref="Languages"/> and one more column to every row (the tests check that no row is missed).
/// {0}, {1} are filled in by <see cref="T(string, object?[])"/>.
///
/// Books bring their own words and language; this is only the app around them.
/// </summary>
public static class Strings
{
    /// <summary>The languages, in the order of the columns below: code, then the language's own name.</summary>
    public static readonly (string Code, string Name)[] Languages = { ("en", "English"), ("af", "Afrikaans") };

    static string _language = "en";

    /// <summary>The language in use now, one of <see cref="Languages"/>.</summary>
    public static string Language
    {
        get => _language;
        set => _language = Index(value) >= 0 ? value : "en";
    }

    static int Index(string? code)
    {
        for (int i = 0; i < Languages.Length; i++) if (Languages[i].Code == code) return i;
        return -1;
    }

    /// <summary>
    /// Which language to use: the one chosen, or with "auto" the device's own when the app speaks it,
    /// otherwise English. <paramref name="device"/> may be "af", "af-ZA", "en_US" and the like.
    /// </summary>
    public static string Resolve(string? chosen, string? device)
    {
        if (Index(chosen) >= 0) return chosen!;
        string two = (device ?? "").Split('-', '_')[0].ToLowerInvariant();
        return Index(two) >= 0 ? two : "en";
    }

    /// <summary>The words for a key in the language in use.</summary>
    public static string T(string key) => For(_language, key);

    public static string T(string key, params object?[] args) => string.Format(System.Globalization.CultureInfo.InvariantCulture, For(_language, key), args);

    /// <summary>The words for a key in a given language. A key that isn't in the table comes back as itself, so a slip shows up on screen rather than crashing.</summary>
    public static string For(string language, string key)
    {
        if (!Table.TryGetValue(key, out string[]? row)) return key;
        int i = Index(language);
        return i >= 0 && i < row.Length ? row[i] : row[0];
    }

    /// <summary>"Lia's books", "Lia se boeke", or "My books" for a child with no name.</summary>
    public static string BooksOf(string? name) => string.IsNullOrWhiteSpace(name) ? T("shelf.myBooks") : T("shelf.plate", name);

    /// <summary>A two-letter tag for a book's language on its cover: "EN", "AF".</summary>
    public static string LanguageTag(string? code) => string.IsNullOrWhiteSpace(code) ? "" : code.Trim().Split('-', '_')[0].ToUpperInvariant();

    /// <summary>A date as people write it, "12 November", in the language in use.</summary>
    public static string Date(DateOnly day) => day.Day.ToString(System.Globalization.CultureInfo.InvariantCulture) + " " + T("month." + day.Month);

    public static IReadOnlyDictionary<string, string[]> All => Table;

    static readonly Dictionary<string, string[]> Table = new()
    {
        // ---- the brand
        ["brand.tagline"] = new[] { "Picture books that move as you read", "Prenteboeke wat beweeg terwyl jy lees" },

        // ---- numbers, for the grown-ups' gate
        ["number.0"] = new[] { "zero", "nul" },
        ["number.1"] = new[] { "one", "een" },
        ["number.2"] = new[] { "two", "twee" },
        ["number.3"] = new[] { "three", "drie" },
        ["number.4"] = new[] { "four", "vier" },
        ["number.5"] = new[] { "five", "vyf" },
        ["number.6"] = new[] { "six", "ses" },
        ["number.7"] = new[] { "seven", "sewe" },
        ["number.8"] = new[] { "eight", "agt" },
        ["number.9"] = new[] { "nine", "nege" },

        // ---- things said in more than one place
        ["common.back"] = new[] { "Back", "Terug" },
        ["common.next"] = new[] { "Next", "Volgende" },
        ["common.close"] = new[] { "Close", "Maak toe" },
        ["common.save"] = new[] { "Save", "Stoor" },
        ["common.on"] = new[] { "On", "Aan" },
        ["common.off"] = new[] { "Off", "Af" },
        ["common.myBooks"] = new[] { "My books", "My boeke" },
        ["common.done"] = new[] { "Done", "Klaar" },

        // ---- the first-run welcome, for the parent
        ["welcome.skip"] = new[] { "Skip", "Slaan oor" },
        ["welcome.1.body"] = new[]
        {
            "The picture glides from one part of the story to the next, like a grown-up pointing as they read. The words can be read aloud, and each word lights up as it is spoken.",
            "Die prent gly van een deel van die storie na die volgende, soos wanneer ’n grootmens wys terwyl hy of sy lees. Die woorde kan hardop gelees word, en elke woord lig op terwyl dit gesê word.",
        },
        ["welcome.2.title"] = new[] { "Made for small hands", "Gemaak vir klein handjies" },
        ["welcome.2.a"] = new[] { "Tap anywhere to go on. The big arrow does the same.", "Tik enige plek om verder te gaan. Die groot pyl doen dieselfde." },
        ["welcome.2.b"] = new[] { "The books button goes back to the shelf, and the place is kept.", "Die boeke-knoppie neem jou terug na die rak, en die plek word onthou." },
        ["welcome.2.c"] = new[] { "Some pages have things to find. No tap is ever wrong.", "Op party bladsye is daar goed om te soek. ’n Tik is nooit verkeerd nie." },
        ["welcome.3.title"] = new[] { "For grown-ups", "Vir grootmense" },
        ["welcome.3.a"] = new[] { "No internet, no adverts, no sign-up. Nothing is collected.", "Geen internet, geen advertensies, geen aanmelding nie. Niks word ingesamel nie." },
        ["welcome.3.b"] = new[] { "The settings sit behind a lock that only someone who can read can open.", "Die instellings is agter ’n slot wat net iemand wat kan lees, kan oopmaak." },
        ["welcome.3.c"] = new[] { "Names and reading progress stay on this device.", "Name en leesvordering bly op hierdie toestel." },
        ["welcome.start"] = new[] { "Set up for my child", "Stel dit op vir my kind" },

        // ---- children's profiles
        ["profile.who"] = new[] { "Who is reading?", "Wie lees?" },
        ["profile.new"] = new[] { "A new reader", "’n Nuwe leser" },
        ["profile.name"] = new[] { "Name or nickname", "Naam of bynaam" },
        ["profile.name.hint"] = new[] { "You can leave this empty. It stays on this device.", "Jy kan dit leeg los. Dit bly op hierdie toestel." },
        ["profile.picture"] = new[] { "Pick a picture", "Kies ’n prentjie" },
        ["profile.age"] = new[] { "Age", "Ouderdom" },
        ["profile.age.hint"] = new[] { "Sets how fast the pictures move and how soon hints appear.", "Bepaal hoe vinnig die prente beweeg en hoe gou wenke verskyn." },
        ["age.3-5"] = new[] { "3 to 5", "3 tot 5" },
        ["age.6-8"] = new[] { "6 to 8", "6 tot 8" },
        ["age.9+"] = new[] { "9 and up", "9 en ouer" },
        ["profile.start"] = new[] { "Start reading", "Begin lees" },
        ["profile.add"] = new[] { "Add a child", "Voeg ’n kind by" },
        ["profile.full"] = new[] { "There is room for five children.", "Daar is plek vir vyf kinders." },
        ["profile.remove"] = new[] { "Remove", "Verwyder" },
        ["profile.remove.ask"] = new[] { "Remove {0} and their reading progress?", "Verwyder {0} se profiel en leesvordering?" },
        ["profile.remove.yes"] = new[] { "Yes, remove", "Ja, verwyder" },
        ["profile.remove.no"] = new[] { "Keep", "Hou dit" },
        ["profile.unnamed"] = new[] { "Reader", "Leser" },
        ["profile.switch"] = new[] { "Who is reading?", "Wie lees?" },
        ["profile.finished.none"] = new[] { "No books finished yet", "Nog geen boeke klaar nie" },
        ["profile.finished.one"] = new[] { "1 book finished", "1 boek klaar gelees" },
        ["profile.finished.many"] = new[] { "{0} books finished", "{0} boeke klaar gelees" },
        ["profile.days.none"] = new[] { "not read this week", "nie hierdie week gelees nie" },
        ["profile.days.one"] = new[] { "read on 1 day this week", "op 1 dag hierdie week gelees" },
        ["profile.days.many"] = new[] { "read on {0} days this week", "op {0} dae hierdie week gelees" },
        ["profile.bookFinished"] = new[] { "Finished {0}", "Klaar gelees op {0}" },
        ["profile.books"] = new[] { "Books read", "Boeke gelees" },
        ["profile.nothingYet"] = new[] { "Nothing read yet.", "Nog niks gelees nie." },
        ["avatar.0"] = new[] { "Sun", "Son" },
        ["avatar.1"] = new[] { "Leaf", "Blaar" },
        ["avatar.2"] = new[] { "Star", "Ster" },
        ["avatar.3"] = new[] { "Drop", "Druppel" },
        ["avatar.4"] = new[] { "Flower", "Blom" },
        ["avatar.5"] = new[] { "Moon", "Maan" },
        ["avatar.6"] = new[] { "Kite", "Vlieër" },
        ["avatar.7"] = new[] { "Heart", "Hart" },

        // ---- the shelf
        ["shelf.plate"] = new[] { "{0}’s books", "{0} se boeke" },
        ["shelf.myBooks"] = new[] { "My books", "My boeke" },
        ["shelf.grownups"] = new[] { "Grown-ups", "Grootmense" },
        ["shelf.carryOn.label"] = new[] { "Carry on reading", "Lees verder" },
        ["shelf.carryOn.button"] = new[] { "Carry on", "Gaan voort" },
        ["shelf.page"] = new[] { "Page {0} of {1}", "Bladsy {0} van {1}" },
        ["shelf.partway"] = new[] { "partly read", "halfpad gelees" },
        ["shelf.finished"] = new[] { "finished", "klaar gelees" },
        ["shelf.empty.title"] = new[] { "No books yet", "Nog geen boeke nie" },
        ["shelf.empty.body"] = new[] { "A grown-up can add a book.", "’n Grootmens kan ’n boek byvoeg." },
        ["shelf.empty.add"] = new[] { "Add a book", "Voeg ’n boek by" },
        ["shelf.opening"] = new[] { "Opening the book…", "Maak die boek oop…" },
        ["shelf.adding"] = new[] { "Adding the book…", "Besig om die boek by te voeg…" },
        ["shelf.gettingReady"] = new[] { "Getting the books ready…", "Kry die boeke gereed…" },
        ["shelf.wontOpen"] = new[] { "This book would not open.", "Hierdie boek wou nie oopmaak nie." },
        ["shelf.someMissing"] = new[] { "Some books could not be shown: {0}", "Party boeke kon nie gewys word nie: {0}" },
        ["shelf.cantLoad"] = new[] { "The books could not be loaded.", "Die boeke kon nie gelaai word nie." },
        ["shelf.notReady"] = new[] { "The books could not be got ready. Close the app and open it again.", "Die boeke kon nie gereed gekry word nie. Maak die app toe en weer oop." },
        ["shelf.noContent"] = new[]
        {
            "No books were found. Put a \"content\" folder beside the program, or add a book file.",
            "Geen boeke is gevind nie. Sit ’n \"content\"-gids langs die program, of voeg ’n boeklêer by.",
        },
        ["shelf.noSuchBook"] = new[] { "The book \"{0}\" could not be found.", "Die boek \"{0}\" kon nie gevind word nie." },
        ["shelf.notABookFolder"] = new[] { "That folder has no book.json in it, so it isn’t a book.", "Daardie gids het nie ’n book.json nie, so dit is nie ’n boek nie." },
        ["shelf.cantOpenFolder"] = new[] { "The folder could not be opened.", "Die gids kon nie oopgemaak word nie." },
        ["shelf.cantListFiles"] = new[] { "The list of files could not be opened.", "Die lys van lêers kon nie oopgemaak word nie." },

        // ---- the reading screen
        ["reader.zoomIn"] = new[] { "Zoom in", "Zoem in" },
        ["reader.zoomOut"] = new[] { "Zoom out", "Zoem uit" },
        ["reader.whole"] = new[] { "Whole page", "Hele bladsy" },
        ["reader.closeUp"] = new[] { "Back to the close-up", "Terug na die nabyskoot" },
        ["reader.showMe"] = new[] { "Show me", "Wys my" },
        ["reader.ask"] = new[] { "ASK", "VRA" },
        ["reader.page"] = new[] { "Page {0} of {1}", "Bladsy {0} van {1}" },
        ["reader.story"] = new[] { "story {0} of {1}", "storie {0} van {1}" },
        ["reader.found"] = new[] { "found {0} of {1}", "{0} van {1} gevind" },

        // ---- The End
        ["end.title"] = new[] { "The End", "Die Einde" },
        ["end.praise"] = new[] { "You read the whole book!", "Jy het die hele boek gelees!" },
        ["end.praise.name"] = new[] { "You read the whole book, {0}!", "Jy het die hele boek gelees, {0}!" },
        ["end.again"] = new[] { "Read it again", "Lees dit weer" },
        ["end.next"] = new[] { "Read next", "Volgende boek" },

        // ---- the grown-ups' gate
        ["gate.title"] = new[] { "Grown-ups only", "Net vir grootmense" },
        ["gate.ask"] = new[] { "Tap these numbers to go on:", "Tik hierdie syfers om voort te gaan:" },
        ["gate.miss"] = new[] { "Not quite. Here are new numbers.", "Nie heeltemal nie. Hier is nuwe syfers." },
        ["gate.rest"] = new[] { "Too many tries. Wait {0} seconds.", "Te veel probeerslae. Wag {0} sekondes." },
        ["gate.delete"] = new[] { "Take one back", "Vee een uit" },

        // ---- the parents' area
        ["parents.title"] = new[] { "For grown-ups", "Vir grootmense" },
        ["parents.children"] = new[] { "Children", "Kinders" },
        ["parents.child"] = new[] { "Child", "Kind" },
        ["parents.app"] = new[] { "The app", "Die app" },
        ["parents.reading"] = new[] { "Reading", "Lees" },
        ["parents.reading.words"] = new[] { "Words and voice", "Woorde en stem" },
        ["parents.reading.pages"] = new[] { "Pages", "Bladsye" },
        ["parents.reading.calm"] = new[] { "Movement and sound", "Beweging en klank" },
        ["parents.reading.age"] = new[]
        {
            "Each child’s age is set on their own page under Children. It sets how fast the pictures move and how soon hints appear.",
            "Elke kind se ouderdom word op sy of haar eie bladsy onder Kinders gestel. Dit bepaal hoe vinnig die prente beweeg en hoe gou wenke verskyn.",
        },
        ["parents.reading.sum"] = new[] { "Words, voice and movement", "Woorde, stem en beweging" },
        ["parents.books"] = new[] { "Books", "Boeke" },
        ["parents.books.sum"] = new[] { "{0} on the shelf", "{0} op die rak" },
        ["parents.language"] = new[] { "Language", "Taal" },
        ["parents.bedtime"] = new[] { "Bedtime colours", "Slaaptydkleure" },
        ["parents.about"] = new[] { "About and privacy", "Oor die app en privaatheid" },
        ["parents.about.sum"] = new[] { "No internet. Nothing collected.", "Geen internet. Niks word ingesamel nie." },

        ["set.readToMe"] = new[] { "Read to me", "Lees vir my" },
        ["set.readToMe.help"] = new[] { "In books with a recorded voice, the story is read aloud and each word lights up.", "In boeke met ’n opgeneemde stem word die storie voorgelees en elke woord lig op." },
        ["set.printedWords"] = new[] { "Words on the page", "Woorde op die bladsy" },
        ["set.printedWords.help"] = new[] { "The words are printed on the picture, as in a paper book.", "Die woorde is op die prent gedruk, soos in ’n papierboek." },
        ["set.popUpWords"] = new[] { "Pop-up words", "Opspringwoorde" },
        ["set.popUpWords.help"] = new[] { "Each part of the story shows in a box at the bottom. Tap a word to hear it.", "Elke deel van die storie verskyn in ’n blokkie onderaan. Tik op ’n woord om dit te hoor." },
        ["set.captionSize"] = new[] { "Size of the pop-up words", "Grootte van die opspringwoorde" },
        ["size.small"] = new[] { "Small", "Klein" },
        ["size.medium"] = new[] { "Medium", "Middel" },
        ["size.large"] = new[] { "Large", "Groot" },
        ["set.prompts"] = new[] { "Questions for the grown-up", "Vrae vir die grootmens" },
        ["set.prompts.help"] = new[] { "A short question at some parts of the story, to get you talking about it together.", "’n Kort vraag by party dele van die storie, sodat julle saam daaroor kan gesels." },
        ["set.extras"] = new[] { "Things to find", "Goed om te soek" },
        ["set.extras.help"] = new[] { "On pages with things to find: the story’s things first, everything at once, or only the story’s.", "Op soekbladsye: eers die storie se goed, alles op een slag, of net die storie se goed." },
        ["extras.on"] = new[] { "Everything", "Alles" },
        ["extras.afterStory"] = new[] { "Story first", "Eers die storie" },
        ["extras.off"] = new[] { "Story only", "Net die storie" },
        ["set.motion"] = new[] { "Movement", "Beweging" },
        ["set.motion.help"] = new[] { "With less movement, pictures fade instead of flying. It helps children who feel dizzy when things zoom.", "Met minder beweging verdof prente in plaas daarvan om te vlieg. Dit help kinders wat duiselig voel wanneer dinge zoem." },
        ["motion.auto"] = new[] { "As the device", "Soos die toestel" },
        ["motion.off"] = new[] { "Full", "Vol" },
        ["motion.on"] = new[] { "Less", "Minder" },
        ["set.sound"] = new[] { "Sounds", "Klanke" },
        ["set.sound.help"] = new[] { "Soft sounds for taps and page turns.", "Sagte klanke wanneer jy tik en blaai." },
        ["set.bookLook"] = new[] { "Show as an open book", "Wys as ’n oop boek" },
        ["set.bookLook.help"] = new[] { "Pictures lie across an open book and the pages curl over. Off: a flat picture.", "Prente lê oor ’n oop boek en die bladsye blaai om. Af: ’n plat prent." },
        ["set.autoPlay"] = new[] { "Watch mode", "Kykmodus" },
        ["set.autoPlay.help"] = new[] { "The story moves on by itself within a book. It never starts another book.", "Die storie gaan vanself verder binne ’n boek. Dit begin nooit ’n ander boek nie." },

        ["books.add"] = new[] { "Add a book file", "Voeg ’n boeklêer by" },
        ["books.add.help.touch"] = new[]
        {
            "Book files end in .picturebook. You can also open one from WhatsApp, Files or an email and choose {0}.",
            "Boeklêers eindig op .picturebook. Jy kan ook een uit WhatsApp, Lêers of ’n e-pos oopmaak en {0} kies.",
        },
        ["books.add.help.desktop"] = new[] { "Book files end in .picturebook. You can also drag one onto this window.", "Boeklêers eindig op .picturebook. Jy kan ook een na hierdie venster sleep." },
        ["books.folder"] = new[] { "Open a book folder", "Maak ’n boekgids oop" },
        ["books.folder.help"] = new[] { "For people who make books: any folder with a book.json in it.", "Vir mense wat boeke maak: enige gids met ’n book.json daarin." },
        ["books.added"] = new[] { "Added from a file", "Bygevoeg uit ’n lêer" },
        ["books.onShelf"] = new[] { "On the shelf", "Op die rak" },
        ["books.builtIn"] = new[] { "Came with the app", "Het saam met die app gekom" },
        ["books.remove"] = new[] { "Remove", "Verwyder" },
        ["books.remove.ask"] = new[] { "Remove \"{0}\" from this device?", "Verwyder \"{0}\" van hierdie toestel?" },
        ["books.remove.yes"] = new[] { "Yes, remove it", "Ja, verwyder dit" },
        ["books.remove.no"] = new[] { "Keep it", "Hou dit" },
        ["books.removed"] = new[] { "Removed \"{0}\".", "\"{0}\" is verwyder." },
        ["books.cantRemove"] = new[] { "That book could not be removed. Try again.", "Daardie boek kon nie verwyder word nie. Probeer weer." },
        ["books.size"] = new[] { "{0} MB", "{0} MB" },
        ["books.pickTitle"] = new[] { "Add a book file", "Voeg ’n boeklêer by" },
        ["books.fileType"] = new[] { "Picture books", "Prenteboeke" },

        ["lang.app"] = new[] { "Language of the app", "Taal van die app" },
        ["lang.device"] = new[] { "As this device ({0})", "Soos hierdie toestel ({0})" },
        ["lang.note"] = new[] { "Books keep their own language. When the shelf holds books in more than one, each cover shows a small tag.", "Boeke behou hul eie taal. As die rak boeke in meer as een taal het, wys elke omslag ’n klein etiket." },

        ["bedtime.help"] = new[] { "Darker and quieter, for reading before sleep.", "Donkerder en rustiger, vir lees voor slaaptyd." },

        ["about.version"] = new[] { "{0}, version {1}", "{0}, weergawe {1}" },
        ["about.privacy"] = new[] { "Privacy", "Privaatheid" },
        ["about.p1"] = new[] { "{0} does not use the internet. It works the same with no signal.", "{0} gebruik nie die internet nie. Dit werk net so goed sonder sein." },
        ["about.p2"] = new[] { "It asks for no permissions: no camera, no microphone, no contacts, no location.", "Dit vra geen toestemmings nie: geen kamera, mikrofoon, kontakte of ligging nie." },
        ["about.p3"] = new[] { "Nothing is collected and nothing leaves this device. There are no accounts and no tracking.", "Niks word ingesamel nie en niks verlaat hierdie toestel nie. Daar is geen rekeninge en geen nasporing nie." },
        ["about.p4"] = new[] { "There are no adverts and nothing to buy.", "Daar is geen advertensies nie en niks om te koop nie." },
        ["about.p5"] = new[]
        {
            "Children’s names, pictures and reading progress are kept only in this app’s own storage. Removing a profile deletes them.",
            "Kinders se name, prentjies en leesvordering word net in hierdie app se eie stoorplek gehou. As jy ’n profiel verwyder, word dit uitgevee.",
        },
        ["about.fonts"] = new[] { "The fonts are Nunito and Fraunces, under the SIL Open Font License.", "Die lettertipes is Nunito en Fraunces, onder die SIL Open Font-lisensie." },
        ["about.welcome"] = new[] { "Show the welcome again", "Wys die verwelkoming weer" },
        ["about.tuning"] = new[] { "Fine tuning for testers", "Fyn verstellings vir toetsers" },

        // ---- months, for dates such as "12 November"
        ["month.1"] = new[] { "January", "Januarie" },
        ["month.2"] = new[] { "February", "Februarie" },
        ["month.3"] = new[] { "March", "Maart" },
        ["month.4"] = new[] { "April", "April" },
        ["month.5"] = new[] { "May", "Mei" },
        ["month.6"] = new[] { "June", "Junie" },
        ["month.7"] = new[] { "July", "Julie" },
        ["month.8"] = new[] { "August", "Augustus" },
        ["month.9"] = new[] { "September", "September" },
        ["month.10"] = new[] { "October", "Oktober" },
        ["month.11"] = new[] { "November", "November" },
        ["month.12"] = new[] { "December", "Desember" },

        // ---- taking in a book file
        ["import.cantRead"] = new[] { "Couldn’t read that file. Try saving it to the phone first, then add it again.", "Kon nie daardie lêer lees nie. Stoor dit eers op die foon en voeg dit dan weer by." },
        ["import.cantReadHere"] = new[] { "Couldn’t read that file. Try saving it on this device first, then add it again.", "Kon nie daardie lêer lees nie. Stoor dit eers op hierdie toestel en voeg dit dan weer by." },
        ["import.tooManyFiles"] = new[] { "That book file has too many files in it to be a picture book.", "Daardie boeklêer het te veel lêers in om ’n prenteboek te wees." },
        ["import.damaged"] = new[]
        {
            "The book in that file is damaged, so it can’t be opened. Export it again from the Book Editor and send the new file.",
            "Die boek in daardie lêer is beskadig en kan nie oopgemaak word nie. Voer dit weer uit die Book Editor uit en stuur die nuwe lêer.",
        },
        ["import.noPages"] = new[] { "The book in that file has no pages yet.", "Die boek in daardie lêer het nog geen bladsye nie." },
        ["import.missing"] = new[]
        {
            "Some pictures are missing from that book ({0}). Export it again from the Book Editor and send the new file.",
            "Party prente ontbreek in daardie boek ({0}). Voer dit weer uit die Book Editor uit en stuur die nuwe lêer.",
        },
        ["import.noRoom"] = new[]
        {
            "There wasn’t room to add that book, or the phone wouldn’t let it be saved. Free up some space and try again.",
            "Daar was nie plek om daardie boek by te voeg nie, of die foon wou dit nie laat stoor nie. Maak ’n bietjie plek oop en probeer weer.",
        },
        ["import.updated"] = new[] { "Updated \"{0}\" on the shelf.", "\"{0}\" is op die rak bygewerk." },
        ["import.added"] = new[] { "Added \"{0}\" to the shelf.", "\"{0}\" is op die rak gesit." },
        ["import.notABook"] = new[]
        {
            "That file isn’t a picture book. Book files end in .picturebook and are made by Export in the Book Editor. If it came from a chat or a download, it may not have finished arriving.",
            "Daardie lêer is nie ’n prenteboek nie. Boeklêers eindig op .picturebook en word met Export in die Book Editor gemaak. As dit uit ’n geselsie of ’n aflaai kom, het dit dalk nog nie klaar aangekom nie.",
        },
        ["import.noBook"] = new[] { "There’s no book in that file. Book files are made by Export in the Book Editor.", "Daar is geen boek in daardie lêer nie. Boeklêers word met Export in die Book Editor gemaak." },
        ["import.twoBooks"] = new[] { "That file holds more than one book. Send each book as a file of its own.", "Daardie lêer bevat meer as een boek. Stuur elke boek as sy eie lêer." },
        ["import.oddNames"] = new[] { "That book file has odd file names in it, so it wasn’t opened, to keep the phone safe.", "Daardie boeklêer het vreemde lêername in, so dit is nie oopgemaak nie, om die foon veilig te hou." },
        ["import.links"] = new[] { "That book file has links to other places in it, so it wasn’t opened, to keep the phone safe.", "Daardie boeklêer het skakels na ander plekke in, so dit is nie oopgemaak nie, om die foon veilig te hou." },
        ["import.tooBig"] = new[] { "That file is too big to be a picture book.", "Daardie lêer is te groot om ’n prenteboek te wees." },
        ["import.tooBigBook"] = new[]
        {
            "That book file is too big for a picture book. Export it again with \"For phones and tablets\" chosen for the picture size.",
            "Daardie boeklêer is te groot vir ’n prenteboek. Voer dit weer uit met \"For phones and tablets\" gekies vir die prentgrootte.",
        },
        ["import.cantHere"] = new[] { "This app can’t add books from files.", "Hierdie app kan nie boeke uit lêers byvoeg nie." },
    };
}
