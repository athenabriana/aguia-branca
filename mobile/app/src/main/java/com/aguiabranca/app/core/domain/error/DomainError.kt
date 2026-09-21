package com.aguiabranca.app.core.domain.error

sealed interface DomainError {
    data class NetworkUnavailable(val cause: Throwable? = null) : DomainError
    data class NotAuthenticated(val message: String? = null) : DomainError
    data class PermissionDenied(val message: String? = null) : DomainError
    data class NotFound(val entity: String, val id: String? = null) : DomainError
    data class ValidationFailed(val field: String, val reason: String) : DomainError
    data class ConflictingState(val message: String) : DomainError
    data class Unknown(val cause: Throwable? = null) : DomainError
}

fun DomainError.toPtBr(): String = when (this) {
    is DomainError.NetworkUnavailable -> "Sem conexão. Verifique sua internet e tente novamente."
    is DomainError.NotAuthenticated -> message ?: "É necessário entrar para continuar."
    is DomainError.PermissionDenied -> message ?: "Você não tem permissão para esta ação."
    is DomainError.NotFound -> "Não encontramos $entity${id?.let { " ($it)" } ?: ""}."
    is DomainError.ValidationFailed -> "Campo \"$field\": $reason"
    is DomainError.ConflictingState -> message
    is DomainError.Unknown -> cause?.localizedMessage ?: "Algo deu errado. Tente novamente."
}
