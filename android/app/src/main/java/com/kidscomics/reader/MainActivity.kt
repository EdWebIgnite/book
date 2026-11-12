package com.kidscomics.reader

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.BackHandler
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.lightColorScheme
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.produceState
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.graphics.Color
import com.kidscomics.reader.data.Book
import com.kidscomics.reader.data.BookRepository
import com.kidscomics.reader.ui.LibraryScreen
import com.kidscomics.reader.ui.ReaderScreen
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext

private val AppColors = lightColorScheme(
    primary = Color(0xFFE8730C),
    secondary = Color(0xFF2F7DE1),
    background = Color(0xFFFFF7EC),
    surface = Color(0xFFFFFFFF),
)

class MainActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        enableEdgeToEdge()
        setContent {
            MaterialTheme(colorScheme = AppColors) {
                val books by produceState(emptyList<Book>()) {
                    value = withContext(Dispatchers.IO) { BookRepository.loadAll(applicationContext) }
                }
                var openBookId by rememberSaveable { mutableStateOf<String?>(null) }
                val openBook = books.firstOrNull { it.id == openBookId }

                if (openBook == null) {
                    LibraryScreen(books, onOpen = { openBookId = it.id })
                } else {
                    BackHandler { openBookId = null }
                    ReaderScreen(openBook, onClose = { openBookId = null })
                }
            }
        }
    }
}
