package com.aguiabranca.app.core.domain.model

/**
 * Catálogo de badges **só para exibição** (nomes exatamente como o servidor os persiste). Quem concede é o servidor
 * (`BadgeEvaluator` do backend, executado a cada evento de pontos); o app apenas mostra as conquistadas em `User.badges`.
 */
object Badges {
    const val PRIMEIRA_IDEIA = "Primeira Ideia"
    const val ESTRATEGISTA = "Estrategista"
    const val INOVADOR_MES = "Inovador do Mês"
    const val IMPACTO_REAL = "Impacto Real"
    const val VISIONARIO = "Visionário"
}
