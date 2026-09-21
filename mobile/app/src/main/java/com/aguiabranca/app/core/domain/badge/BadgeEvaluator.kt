package com.aguiabranca.app.core.domain.badge

import com.aguiabranca.app.core.domain.model.Idea
import com.aguiabranca.app.core.domain.model.IdeaStatus
import com.aguiabranca.app.core.domain.model.User
import java.util.Calendar

object Badges {
    const val PRIMEIRA_IDEIA = "Primeira Ideia"
    const val ESTRATEGISTA = "Estrategista"
    const val INOVADOR_MES = "Inovador do Mês"
    const val IMPACTO_REAL = "Impacto Real"
    const val VISIONARIO = "Visionário"
}

object BadgeEvaluator {

    fun evaluate(user: User, ideas: List<Idea>): Set<String> {
        val authoredIdeas = ideas.filter { it.authorId == user.id }
        val already = user.badges.toSet()
        val unlocked = mutableSetOf<String>()

        if (authoredIdeas.isNotEmpty() && Badges.PRIMEIRA_IDEIA !in already) {
            unlocked += Badges.PRIMEIRA_IDEIA
        }

        if (Badges.ESTRATEGISTA !in already &&
            authoredIdeas.any { it.guidelineId != null && it.status in approvedOrLater }
        ) {
            unlocked += Badges.ESTRATEGISTA
        }

        if (Badges.INOVADOR_MES !in already) {
            val groupedByMonth = authoredIdeas.groupBy { yearMonthOf(it.createdAt) }
            if (groupedByMonth.any { it.value.size >= 5 }) {
                unlocked += Badges.INOVADOR_MES
            }
        }

        if (Badges.IMPACTO_REAL !in already &&
            authoredIdeas.any { it.status == IdeaStatus.IMPLEMENTADA }
        ) {
            unlocked += Badges.IMPACTO_REAL
        }

        if (Badges.VISIONARIO !in already) {
            val distinctGuidelines = authoredIdeas
                .filter { it.guidelineId != null && it.status in approvedOrLater }
                .mapNotNull { it.guidelineId }
                .toSet()
            if (distinctGuidelines.size >= 3) {
                unlocked += Badges.VISIONARIO
            }
        }

        return unlocked
    }

    private val approvedOrLater = setOf(IdeaStatus.APROVADA, IdeaStatus.IMPLEMENTADA)

    private fun yearMonthOf(epochMillis: Long): Int {
        if (epochMillis <= 0) return 0
        val c = Calendar.getInstance().apply { timeInMillis = epochMillis }
        return c.get(Calendar.YEAR) * 100 + c.get(Calendar.MONTH)
    }
}
