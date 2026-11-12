package com.kidscomics.reader.ui

import android.content.Context
import androidx.compose.ui.geometry.Rect
import com.kidscomics.reader.data.Book
import com.kidscomics.reader.data.BookRepository

/** Space between pages, as a fraction of page height. */
private const val PAGE_GAP = 0.12f

/** Breathing room around a focused panel, as a fraction of page height. */
private const val PANEL_PADDING = 0.025f

/** Breathing room around a whole page. */
private const val PAGE_PADDING = 0.03f

/** One stop of the camera. [panel] is -1 when the whole page is shown. */
data class Step(val page: Int, val panel: Int) {
    val isWholePage get() = panel < 0
}

/**
 * Lays every page of the book out in a row in "world" coordinates, where each
 * page is 1.0 tall and as wide as its aspect ratio. The camera then just moves
 * between rectangles in this one space, which makes page changes a smooth
 * zoom-out-and-across instead of a hard cut.
 */
class BookLayout private constructor(
    val book: Book,
    val pageRects: List<Rect>,
) {
    /** Reading order: whole page first, then each panel, then the next page. */
    val steps: List<Step> = book.pages.flatMapIndexed { pageIndex, page ->
        listOf(Step(pageIndex, -1)) + page.panels.indices.map { Step(pageIndex, it) }
    }

    fun panelRect(page: Int, panel: Int): Rect {
        val pr = pageRects[page]
        val p = book.pages[page].panels[panel]
        return Rect(
            left = pr.left + p.x * pr.width,
            top = pr.top + p.y * pr.height,
            right = pr.left + (p.x + p.w) * pr.width,
            bottom = pr.top + (p.y + p.h) * pr.height,
        )
    }

    /** The bright, undimmed area for a step. */
    fun focus(step: Step): Rect =
        if (step.isWholePage) pageRects[step.page] else panelRect(step.page, step.panel)

    /** What the camera should fit on screen for a step. */
    fun camera(step: Step): Rect =
        if (step.isWholePage) wholePageCamera(step.page) else focus(step).inflate(PANEL_PADDING)

    fun wholePageCamera(page: Int): Rect = pageRects[page].inflate(PAGE_PADDING)

    companion object {
        /** Reads image sizes (not pixels) so pages keep their real proportions. Call off the main thread. */
        fun create(context: Context, book: Book): BookLayout {
            var x = 0f
            val rects = book.pages.map { page ->
                val (w, h) = BookRepository.imageSize(context, book, page.image)
                val aspect = if (w > 0 && h > 0) w.toFloat() / h else 2f / 3f
                val r = Rect(x, 0f, x + aspect, 1f)
                x += aspect + PAGE_GAP
                r
            }
            return BookLayout(book, rects)
        }
    }
}
