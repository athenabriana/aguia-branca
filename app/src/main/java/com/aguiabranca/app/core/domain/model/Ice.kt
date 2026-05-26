package com.aguiabranca.app.core.domain.model

data class Ice(
    val impact: Int,
    val confidence: Int,
    val ease: Int
) {
    val score: Int = impact * confidence * ease
    val isComplete: Boolean = impact in 1..10 && confidence in 1..10 && ease in 1..10
}
