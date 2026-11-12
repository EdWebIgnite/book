package com.kidscomics.reader.data

import android.content.Context
import android.graphics.Bitmap
import android.graphics.BitmapFactory
import android.util.Log
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import org.json.JSONObject

/** A panel box, in fractions (0..1) of the page image's width and height. */
data class Panel(val x: Float, val y: Float, val w: Float, val h: Float)

/** One page: an image plus its panels in reading order. A page with no panels is shown whole. */
data class Page(val image: String, val panels: List<Panel>)

data class Book(
    val id: String,
    val title: String,
    val author: String,
    val cover: String,
    val pages: List<Page>,
    /** Folder under assets/, e.g. "books/zolas-kite". */
    val dir: String,
)

/**
 * Runs slow file work off the main thread and always comes back on the main thread,
 * whatever dispatcher the caller is on, so screen state is only ever changed from there.
 */
suspend fun <T> loadInBackground(block: () -> T): T =
    withContext(Dispatchers.Main.immediate) { withContext(Dispatchers.IO) { block() } }

/** Reads books bundled in assets/books/<folder>/book.json. */
object BookRepository {
    private const val TAG = "BookRepository"
    private const val ROOT = "books"

    fun loadAll(context: Context): List<Book> =
        // substringBefore: some asset implementations (e.g. Robolectric) list nested paths, not folders.
        context.assets.list(ROOT).orEmpty().map { it.substringBefore('/') }.distinct().mapNotNull { folder ->
            val dir = "$ROOT/$folder"
            try {
                val json = context.assets.open("$dir/book.json").bufferedReader().use { it.readText() }
                parse(json, dir)
            } catch (e: Exception) {
                Log.w(TAG, "Skipping $dir: ${e.message}")
                null
            }
        }.sortedBy { it.title }

    fun parse(json: String, dir: String): Book {
        val o = JSONObject(json)
        val pagesJson = o.getJSONArray("pages")
        val pages = (0 until pagesJson.length()).map { i ->
            val p = pagesJson.getJSONObject(i)
            // Current format: "stops", each with a "box". Older files: "panels", plain boxes.
            // The app only uses each stop's box for now; pans, styles and captions are web-only until
            // the movement is settled in the playground.
            val stops = p.optJSONArray("stops")
            val legacy = p.optJSONArray("panels")
            val boxes = when {
                stops != null -> (0 until stops.length()).map { stops.getJSONObject(it).getJSONObject("box") }
                legacy != null -> (0 until legacy.length()).map { legacy.getJSONObject(it) }
                else -> emptyList()
            }
            val panels = boxes.map { b ->
                Panel(
                    x = b.getDouble("x").toFloat(),
                    y = b.getDouble("y").toFloat(),
                    w = b.getDouble("w").toFloat(),
                    h = b.getDouble("h").toFloat(),
                )
            }
            Page(image = p.getString("image"), panels = panels)
        }
        return Book(
            id = o.getString("id"),
            title = o.getString("title"),
            author = o.optString("author"),
            cover = o.optString("cover", pages.firstOrNull()?.image ?: ""),
            pages = pages,
            dir = dir,
        )
    }

    /** Image width and height without decoding the pixels. */
    fun imageSize(context: Context, book: Book, file: String): Pair<Int, Int> {
        val opts = BitmapFactory.Options().apply { inJustDecodeBounds = true }
        context.assets.open("${book.dir}/$file").use { BitmapFactory.decodeStream(it, null, opts) }
        return opts.outWidth to opts.outHeight
    }

    /** Decodes an image, downsampling so its longest side is at most [maxSide] pixels. */
    fun loadImage(context: Context, book: Book, file: String, maxSide: Int): Bitmap? {
        val (w, h) = imageSize(context, book, file)
        var sample = 1
        while (maxOf(w, h) / (sample * 2) >= maxSide) sample *= 2
        val opts = BitmapFactory.Options().apply { inSampleSize = sample }
        return context.assets.open("${book.dir}/$file").use { BitmapFactory.decodeStream(it, null, opts) }
    }
}
