package com.kidscomics.reader.ui

import android.app.Activity
import android.content.Context
import android.content.ContextWrapper
import android.graphics.Bitmap
import android.graphics.Paint
import androidx.compose.animation.AnimatedVisibility
import androidx.compose.animation.core.Animatable
import androidx.compose.animation.core.FastOutSlowInEasing
import androidx.compose.animation.core.VectorConverter
import androidx.compose.animation.core.tween
import androidx.compose.animation.fadeIn
import androidx.compose.animation.fadeOut
import androidx.compose.foundation.Canvas
import androidx.compose.foundation.background
import androidx.compose.foundation.gestures.awaitEachGesture
import androidx.compose.foundation.gestures.awaitFirstDown
import androidx.compose.foundation.gestures.calculatePan
import androidx.compose.foundation.gestures.calculateZoom
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.WindowInsets
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.safeDrawing
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.windowInsetsPadding
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.automirrored.filled.KeyboardArrowLeft
import androidx.compose.material.icons.automirrored.filled.KeyboardArrowRight
import androidx.compose.material3.Button
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.FilledTonalButton
import androidx.compose.material3.FilledTonalIconButton
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.IconButtonDefaults
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateMapOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.produceState
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.geometry.CornerRadius
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.Rect
import androidx.compose.ui.geometry.RoundRect
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.Path
import androidx.compose.ui.graphics.PathFillType
import androidx.compose.ui.graphics.drawscope.drawIntoCanvas
import androidx.compose.ui.graphics.nativeCanvas
import androidx.compose.ui.graphics.toAndroidRectF
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.ui.input.pointer.positionChanged
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.LocalView
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.core.view.WindowCompat
import androidx.core.view.WindowInsetsCompat
import androidx.core.view.WindowInsetsControllerCompat
import com.kidscomics.reader.data.Book
import com.kidscomics.reader.data.BookRepository
import com.kidscomics.reader.data.loadInBackground
import kotlinx.coroutines.launch
import kotlin.math.abs
import kotlin.math.min

private val Backdrop = Color(0xFF1C1B22)
private const val MOVE_MILLIS = 650
private const val DIM_ALPHA = 0.72f

/** Longest side, in pixels, that page images are decoded at. */
private const val MAX_PAGE_PIXELS = 2600

@Composable
fun ReaderScreen(book: Book, onClose: () -> Unit) {
    val context = LocalContext.current
    HideSystemBars()

    val layout by produceState<BookLayout?>(null, book) {
        value = loadInBackground { BookLayout.create(context, book) }
    }

    val ready = layout
    if (ready == null) {
        Box(Modifier.fillMaxSize().background(Backdrop), contentAlignment = Alignment.Center) {
            CircularProgressIndicator()
        }
    } else {
        Reader(ready, onClose)
    }
}

@Composable
private fun Reader(layout: BookLayout, onClose: () -> Unit) {
    val context = LocalContext.current
    val book = layout.book
    val scope = rememberCoroutineScope()

    var stepIndex by rememberSaveable(book.id) { mutableIntStateOf(0) }
    var overview by rememberSaveable(book.id) { mutableStateOf(false) }
    var finished by rememberSaveable(book.id) { mutableStateOf(false) }

    val step = layout.steps[stepIndex.coerceIn(layout.steps.indices)]
    val showWholePage = overview || step.isWholePage

    // Where the camera and the bright "spotlight" should end up for this step.
    val targetCamera = if (overview) layout.wholePageCamera(step.page) else layout.camera(step)
    val targetFocus = if (overview) layout.pageRects[step.page] else layout.focus(step)
    val targetDim = if (showWholePage) 0f else DIM_ALPHA

    val camera = remember { Animatable(targetCamera, Rect.VectorConverter) }
    val focus = remember { Animatable(targetFocus, Rect.VectorConverter) }
    val dim = remember { Animatable(targetDim) }

    LaunchedEffect(targetCamera, targetFocus, targetDim) {
        launch { camera.animateTo(targetCamera, tween(MOVE_MILLIS, easing = FastOutSlowInEasing)) }
        launch { focus.animateTo(targetFocus, tween(MOVE_MILLIS, easing = FastOutSlowInEasing)) }
        launch { dim.animateTo(targetDim, tween(MOVE_MILLIS)) }
    }

    // Keep only the current page and its neighbours in memory.
    val bitmaps = remember(book.id) { mutableStateMapOf<Int, Bitmap>() }
    LaunchedEffect(book.id, step.page) {
        val wanted = listOf(step.page, step.page + 1, step.page - 1).filter { it in book.pages.indices }
        bitmaps.keys.filter { it !in wanted }.forEach { bitmaps.remove(it) }
        for (i in wanted) {
            if (i in bitmaps) continue
            val bmp = loadInBackground {
                BookRepository.loadImage(context, book, book.pages[i].image, MAX_PAGE_PIXELS)
            }
            if (bmp != null) bitmaps[i] = bmp
        }
    }

    fun next() {
        overview = false
        if (stepIndex < layout.steps.lastIndex) stepIndex++ else finished = true
    }

    fun previous() {
        overview = false
        if (stepIndex > 0) stepIndex--
    }

    val paint = remember { Paint(Paint.FILTER_BITMAP_FLAG or Paint.ANTI_ALIAS_FLAG) }
    val dimPath = remember { Path() }

    Box(Modifier.fillMaxSize().background(Backdrop)) {
        Canvas(
            Modifier
                .fillMaxSize()
                .pointerInput(layout) {
                    // Tap right to go on, tap left to go back, swipe either way,
                    // pinch in for the whole page and pinch out to go back to panels.
                    awaitEachGesture {
                        val down = awaitFirstDown(requireUnconsumed = false)
                        var zoom = 1f
                        var pan = Offset.Zero
                        var multiTouch = false
                        do {
                            val event = awaitPointerEvent()
                            if (event.changes.count { it.pressed } > 1) multiTouch = true
                            zoom *= event.calculateZoom()
                            pan += event.calculatePan()
                            event.changes.forEach { if (it.positionChanged()) it.consume() }
                        } while (event.changes.any { it.pressed })

                        val swipe = 48.dp.toPx()
                        when {
                            multiTouch && zoom < 0.85f -> overview = true
                            multiTouch && zoom > 1.15f -> overview = false
                            multiTouch -> Unit
                            abs(pan.x) > swipe && abs(pan.x) > abs(pan.y) ->
                                if (pan.x < 0) next() else previous()
                            pan.getDistance() < viewConfiguration.touchSlop ->
                                if (down.position.x < size.width * 0.3f) previous() else next()
                        }
                    }
                },
        ) {
            // Leave room for the top and bottom controls when fitting the camera.
            val topInset = 64.dp.toPx()
            val bottomInset = 88.dp.toPx()
            val viewH = (size.height - topInset - bottomInset).coerceAtLeast(1f)
            val cam = camera.value
            val scale = min(size.width / cam.width, viewH / cam.height)
            val ox = size.width / 2f - cam.center.x * scale
            val oy = topInset + viewH / 2f - cam.center.y * scale

            fun Rect.toScreen() = Rect(left * scale + ox, top * scale + oy, right * scale + ox, bottom * scale + oy)

            layout.pageRects.forEachIndexed { i, worldRect ->
                val r = worldRect.toScreen()
                if (r.right < 0f || r.left > size.width) return@forEachIndexed
                drawRect(Color.White, r.topLeft, r.size)
                bitmaps[i]?.let { bmp ->
                    drawIntoCanvas { it.nativeCanvas.drawBitmap(bmp, null, r.toAndroidRectF(), paint) }
                }
            }

            // Dim everything outside the current panel so the eye goes straight to it.
            if (dim.value > 0.01f) {
                val hole = focus.value.toScreen()
                dimPath.reset()
                dimPath.fillType = PathFillType.EvenOdd
                dimPath.addRect(Rect(Offset.Zero, size))
                dimPath.addRoundRect(RoundRect(hole, CornerRadius(6.dp.toPx())))
                drawPath(dimPath, Color.Black.copy(alpha = dim.value))
            }
        }

        TopBar(
            title = book.title,
            onClose = onClose,
            modifier = Modifier.align(Alignment.TopCenter),
        )

        BottomBar(
            page = step.page + 1,
            pageCount = book.pages.size,
            showingWholePage = showWholePage,
            canGoBack = stepIndex > 0,
            onPrevious = ::previous,
            onNext = ::next,
            onToggleWholePage = {
                if (step.isWholePage && !overview) next() else overview = !overview
            },
            modifier = Modifier.align(Alignment.BottomCenter),
        )

        AnimatedVisibility(
            visible = finished,
            enter = fadeIn(),
            exit = fadeOut(),
            modifier = Modifier.fillMaxSize(),
        ) {
            TheEnd(
                onReadAgain = {
                    finished = false
                    overview = false
                    stepIndex = 0
                    scope.launch { camera.snapTo(layout.camera(layout.steps.first())) }
                },
                onClose = onClose,
            )
        }
    }
}

@Composable
private fun TopBar(title: String, onClose: () -> Unit, modifier: Modifier = Modifier) {
    Row(
        modifier
            .fillMaxWidth()
            .windowInsetsPadding(WindowInsets.safeDrawing)
            .padding(horizontal = 8.dp, vertical = 6.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        FilledTonalIconButton(onClick = onClose, modifier = Modifier.size(48.dp)) {
            Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = "Back to books")
        }
        Spacer(Modifier.size(12.dp))
        Text(title, color = Color.White, fontSize = 20.sp, fontWeight = FontWeight.Bold)
    }
}

@Composable
private fun BottomBar(
    page: Int,
    pageCount: Int,
    showingWholePage: Boolean,
    canGoBack: Boolean,
    onPrevious: () -> Unit,
    onNext: () -> Unit,
    onToggleWholePage: () -> Unit,
    modifier: Modifier = Modifier,
) {
    Row(
        modifier
            .fillMaxWidth()
            .windowInsetsPadding(WindowInsets.safeDrawing)
            .padding(horizontal = 12.dp, vertical = 10.dp),
        verticalAlignment = Alignment.CenterVertically,
        horizontalArrangement = Arrangement.SpaceBetween,
    ) {
        BigArrow(Icons.AutoMirrored.Filled.KeyboardArrowLeft, "Back", enabled = canGoBack, onClick = onPrevious)
        Column(horizontalAlignment = Alignment.CenterHorizontally) {
            FilledTonalButton(onClick = onToggleWholePage) {
                Text(if (showingWholePage) "Panels" else "Whole page", fontSize = 16.sp)
            }
            Text("Page $page of $pageCount", color = Color.White.copy(alpha = 0.8f), fontSize = 13.sp)
        }
        BigArrow(Icons.AutoMirrored.Filled.KeyboardArrowRight, "Next", enabled = true, onClick = onNext)
    }
}

@Composable
private fun BigArrow(
    icon: androidx.compose.ui.graphics.vector.ImageVector,
    label: String,
    enabled: Boolean,
    onClick: () -> Unit,
) {
    IconButton(
        onClick = onClick,
        enabled = enabled,
        modifier = Modifier.size(64.dp),
        colors = IconButtonDefaults.iconButtonColors(
            containerColor = Color.White.copy(alpha = 0.15f),
            contentColor = Color.White,
            disabledContainerColor = Color.White.copy(alpha = 0.05f),
            disabledContentColor = Color.White.copy(alpha = 0.3f),
        ),
    ) {
        Icon(icon, contentDescription = label, modifier = Modifier.size(40.dp))
    }
}

@Composable
private fun TheEnd(onReadAgain: () -> Unit, onClose: () -> Unit) {
    Box(
        Modifier
            .fillMaxSize()
            .background(Color.Black.copy(alpha = 0.6f))
            // Swallow touches so the reader underneath doesn't react.
            .pointerInput(Unit) { awaitEachGesture { awaitFirstDown().consume() } },
        contentAlignment = Alignment.Center,
    ) {
        Surface(shape = RoundedCornerShape(24.dp), tonalElevation = 6.dp) {
            Column(
                Modifier.padding(horizontal = 40.dp, vertical = 32.dp),
                horizontalAlignment = Alignment.CenterHorizontally,
            ) {
                Text("The End", fontSize = 40.sp, fontWeight = FontWeight.Black)
                Spacer(Modifier.height(24.dp))
                Button(onClick = onReadAgain, shape = CircleShape) { Text("Read again", fontSize = 18.sp) }
                Spacer(Modifier.height(12.dp))
                FilledTonalButton(onClick = onClose, shape = CircleShape) { Text("Back to books", fontSize = 18.sp) }
            }
        }
    }
}

/** Full-screen reading: hide the status and navigation bars while the reader is open. */
@Composable
private fun HideSystemBars() {
    val view = LocalView.current
    DisposableEffect(view) {
        val window = view.context.findActivity()?.window
        val controller = window?.let { WindowCompat.getInsetsController(it, view) }
        controller?.systemBarsBehavior = WindowInsetsControllerCompat.BEHAVIOR_SHOW_TRANSIENT_BARS_BY_SWIPE
        controller?.hide(WindowInsetsCompat.Type.systemBars())
        onDispose { controller?.show(WindowInsetsCompat.Type.systemBars()) }
    }
}

private tailrec fun Context.findActivity(): Activity? = when (this) {
    is Activity -> this
    is ContextWrapper -> baseContext.findActivity()
    else -> null
}
