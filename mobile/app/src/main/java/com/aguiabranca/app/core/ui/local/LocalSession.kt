package com.aguiabranca.app.core.ui.local

import androidx.compose.runtime.Composable
import androidx.compose.runtime.compositionLocalOf
import com.aguiabranca.app.core.domain.model.AuthSession

val LocalSession = compositionLocalOf<AuthSession?> { null }

@Composable
fun requireSession(): AuthSession =
    LocalSession.current ?: error("LocalSession not provided — auth-only screen accessed without session.")
