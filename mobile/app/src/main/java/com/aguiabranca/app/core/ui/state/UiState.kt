package com.aguiabranca.app.core.ui.state

import com.aguiabranca.app.core.domain.error.DomainError

sealed interface UiState<out T> {
    data object Idle : UiState<Nothing>
    data object Loading : UiState<Nothing>
    data class Success<T>(val data: T) : UiState<T>
    data class Error(val error: DomainError) : UiState<Nothing>
}
