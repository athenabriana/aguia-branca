package com.aguiabranca.app.core.ui.theme

import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.lightColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.runtime.Immutable
import androidx.compose.runtime.ReadOnlyComposable
import androidx.compose.runtime.staticCompositionLocalOf
import androidx.compose.ui.graphics.Color

@Immutable
data class SemanticColors(
    val success: Color,
    val warning: Color,
    val danger: Color,
    val info: Color
)

private val DefaultSemanticColors = SemanticColors(
    success = SemanticSuccess,
    warning = SemanticWarning,
    danger = SemanticDanger,
    info = SemanticInfo
)

private val LocalSemanticColors = staticCompositionLocalOf { DefaultSemanticColors }

val MaterialTheme.semantic: SemanticColors
    @Composable @ReadOnlyComposable
    get() = LocalSemanticColors.current

private val AguiaBrancaColorScheme = lightColorScheme(
    primary = BrandPrimary,
    onPrimary = Color.White,
    primaryContainer = BrandPrimaryDark,
    onPrimaryContainer = Color.White,
    secondary = BrandSecondary,
    onSecondary = Color(0xFF222222),
    background = Color.White,
    onBackground = BrandOnSurface,
    surface = BrandSurface,
    onSurface = BrandOnSurface,
    surfaceVariant = NeutralGray100,
    onSurfaceVariant = NeutralGray700,
    outline = NeutralGray300,
    error = SemanticDanger,
    onError = Color.White
)

@Composable
fun AguiaBrancaTheme(content: @Composable () -> Unit) {
    MaterialTheme(
        colorScheme = AguiaBrancaColorScheme,
        typography = AguiaBrancaTypography,
        content = content
    )
}
