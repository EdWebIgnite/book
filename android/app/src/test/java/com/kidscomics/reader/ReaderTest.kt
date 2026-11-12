package com.kidscomics.reader

import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.test.junit4.createComposeRule
import androidx.compose.ui.test.onAllNodesWithText
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.onRoot
import androidx.compose.ui.test.performClick
import androidx.compose.ui.test.performTouchInput
import androidx.compose.ui.test.click
import androidx.compose.ui.test.pinch
import com.github.takahirom.roborazzi.captureRoboImage
import com.kidscomics.reader.data.BookRepository
import com.kidscomics.reader.ui.BookLayout
import com.kidscomics.reader.ui.LibraryScreen
import com.kidscomics.reader.ui.ReaderScreen
import com.kidscomics.reader.ui.Step
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.RobolectricTestRunner
import org.robolectric.RuntimeEnvironment
import org.robolectric.annotation.Config
import org.robolectric.annotation.GraphicsMode

@RunWith(RobolectricTestRunner::class)
@GraphicsMode(GraphicsMode.Mode.NATIVE)
@Config(sdk = [34], qualifiers = "w400dp-h860dp-xhdpi")
class ReaderTest {

    @get:Rule
    val rule = createComposeRule()

    private val context get() = RuntimeEnvironment.getApplication()
    private val book get() = BookRepository.loadAll(context).first { it.id == "zolas-kite" }

    @Test
    fun stepsGoWholePageThenEachPanel() {
        val layout = BookLayout.create(context, book)
        val panelsPerPage = book.pages.map { it.panels.size }
        assertEquals(panelsPerPage.sum() + book.pages.size, layout.steps.size)
        assertEquals(Step(0, -1), layout.steps[0])
        assertEquals(Step(0, 0), layout.steps[1])
        assertEquals(Step(1, -1), layout.steps[1 + panelsPerPage[0]])

        // Every panel sits inside its page, and pages don't overlap.
        layout.steps.filter { !it.isWholePage }.forEach { s ->
            val page = layout.pageRects[s.page]
            val panel = layout.panelRect(s.page, s.panel)
            assertTrue(panel.left >= page.left && panel.right <= page.right)
            assertTrue(panel.top >= page.top && panel.bottom <= page.bottom)
        }
        layout.pageRects.zipWithNext { a, b -> assertTrue(a.right < b.left) }
    }

    @Test
    fun readsStopsAndOldPanelsFormat() {
        val stops = BookRepository.parse(
            """{"id":"a","title":"A","pages":[{"image":"p.webp","stops":[{"box":{"x":0.1,"y":0.2,"w":0.3,"h":0.4},"caption":"Hi"}]}]}""",
            "books/a",
        )
        val panels = BookRepository.parse(
            """{"id":"b","title":"B","pages":[{"image":"p.webp","panels":[{"x":0.1,"y":0.2,"w":0.3,"h":0.4}]}]}""",
            "books/b",
        )
        assertEquals(stops.pages[0].panels, panels.pages[0].panels)
        assertEquals(0.3f, stops.pages[0].panels[0].w)
    }

    @Test
    fun screenshots() {
        rule.setContent { LibraryScreen(BookRepository.loadAll(context), onOpen = {}) }
        settle()
        rule.onRoot().captureRoboImage("screenshots/01_library.png")
    }

    @Test
    fun readerWalkthrough() {
        rule.setContent { ReaderScreen(book, onClose = {}) }
        rule.waitUntil(10_000) { rule.onAllNodesWithText("Panels").fetchSemanticsNodes().isNotEmpty() }
        settle()
        shot("02_page1_whole")

        tapRight()
        shot("03_page1_panel1")

        tapRight()
        shot("04_page1_panel2")

        // Pinch in to see the whole page, then carry on.
        rule.onRoot().performTouchInput {
            pinch(
                start0 = center + Offset(-300f, -300f), end0 = center + Offset(-40f, -40f),
                start1 = center + Offset(300f, 300f), end1 = center + Offset(40f, 40f),
            )
        }
        settle()
        shot("05_pinched_to_whole_page")

        // Next from the overview continues to panel 3, then panel 4, then page 2.
        tapRight()
        tapRight()
        shot("06_page1_panel4")
        tapRight()
        shot("07_page2_whole")

        tapRight()
        shot("08_page2_panel1")

        rule.onNodeWithText("Whole page").performClick()
        settle()
        shot("09_whole_page_button")
    }

    private fun tapRight() {
        rule.onRoot().performTouchInput { click(Offset(width * 0.8f, height * 0.5f)) }
        settle()
    }

    /** Lets background image loading and camera animations finish. */
    private fun settle() {
        repeat(3) {
            Thread.sleep(250)
            rule.mainClock.advanceTimeBy(1_000)
            rule.waitForIdle()
        }
    }

    private fun shot(name: String) = rule.onRoot().captureRoboImage("screenshots/$name.png")
}
