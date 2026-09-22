package com.aguiabranca.app.core.domain.error

sealed interface DomainError {
    data class NetworkUnavailable(val cause: Throwable? = null) : DomainError
    data class NotAuthenticated(val message: String? = null) : DomainError
    data class PermissionDenied(val message: String? = null) : DomainError
    data class NotFound(val entity: String, val id: String? = null) : DomainError
    data class ValidationFailed(val field: String, val reason: String) : DomainError
    data class ConflictingState(val message: String) : DomainError
    /** 429: limite de requisições (por usuário ou cota diária da IA). */
    data class TooManyRequests(val message: String? = null, val retryAfterSeconds: Int? = null) : DomainError
    /** 502/503 de dependência externa (ex.: IA indisponível ou com resposta inválida). Nunca se exibe conteúdo no lugar. */
    data class ServiceUnavailable(val code: String? = null, val message: String? = null) : DomainError
    data class Unknown(val cause: Throwable? = null) : DomainError
}

fun DomainError.toPtBr(): String = when (this) {
    is DomainError.NetworkUnavailable -> "Sem conexão. Verifique sua internet e tente novamente."
    is DomainError.NotAuthenticated -> message ?: "É necessário entrar para continuar."
    is DomainError.PermissionDenied -> message ?: "Você não tem permissão para esta ação."
    is DomainError.NotFound -> "Não encontramos $entity${id?.let { " ($it)" } ?: ""}."
    is DomainError.ValidationFailed -> "Campo \"$field\": $reason"
    is DomainError.ConflictingState -> message
    is DomainError.TooManyRequests -> message ?: "Muitas tentativas. Aguarde um pouco e tente novamente."
    is DomainError.ServiceUnavailable -> message ?: "Serviço indisponível no momento. Tente novamente em instantes."
    is DomainError.Unknown -> cause?.localizedMessage ?: "Algo deu errado. Tente novamente."
}
