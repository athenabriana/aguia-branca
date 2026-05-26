package com.aguiabranca.app.core.domain.badge

import com.aguiabranca.app.core.domain.model.Division
import com.aguiabranca.app.core.domain.model.Idea
import com.aguiabranca.app.core.domain.model.IdeaStatus
import com.aguiabranca.app.core.domain.model.Role
import com.aguiabranca.app.core.domain.model.User
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test
import java.util.Calendar

class BadgeEvaluatorTest {

    private val user = User("u1", "Op", "o@x.com", Role.OPERADOR, Division.LOGISTICA, 0, emptyList(), 0L)

    private fun idea(
        id: String,
        status: IdeaStatus = IdeaStatus.SUBMETIDA,
        guideline: String? = null,
        createdAt: Long = System.currentTimeMillis(),
        author: String = "u1"
    ) = Idea(
        id = id, title = "t$id", description = "d", category = "c",
        division = Division.LOGISTICA, guidelineId = guideline,
        authorId = author, authorName = "n", status = status,
        ice = null, reviewerId = null, reviewComment = null,
        createdAt = createdAt, reviewedAt = null
    )

    @Test fun primeira_ideia_unlocks_on_first_submission() {
        val unlocked = BadgeEvaluator.evaluate(user, listOf(idea("i1")))
        assertTrue(Badges.PRIMEIRA_IDEIA in unlocked)
    }

    @Test fun estrategista_requires_guideline_and_approval() {
        val ideas = listOf(idea("i1", IdeaStatus.APROVADA, guideline = "g1"))
        val unlocked = BadgeEvaluator.evaluate(user, ideas)
        assertTrue(Badges.ESTRATEGISTA in unlocked)
    }

    @Test fun estrategista_blocked_without_guideline() {
        val ideas = listOf(idea("i1", IdeaStatus.APROVADA, guideline = null))
        val unlocked = BadgeEvaluator.evaluate(user, ideas)
        assertFalse(Badges.ESTRATEGISTA in unlocked)
    }

    @Test fun inovador_do_mes_with_five_in_same_month() {
        val base = Calendar.getInstance().apply { set(2026, Calendar.MAY, 5) }.timeInMillis
        val ideas = (1..5).map { idea("i$it", createdAt = base + it * 1000L) }
        val unlocked = BadgeEvaluator.evaluate(user, ideas)
        assertTrue(Badges.INOVADOR_MES in unlocked)
    }

    @Test fun inovador_do_mes_blocked_when_split_across_months() {
        val mai = Calendar.getInstance().apply { set(2026, Calendar.MAY, 5) }.timeInMillis
        val jun = Calendar.getInstance().apply { set(2026, Calendar.JUNE, 5) }.timeInMillis
        val ideas = listOf(
            idea("i1", createdAt = mai), idea("i2", createdAt = mai), idea("i3", createdAt = mai),
            idea("i4", createdAt = jun), idea("i5", createdAt = jun)
        )
        val unlocked = BadgeEvaluator.evaluate(user, ideas)
        assertFalse(Badges.INOVADOR_MES in unlocked)
    }

    @Test fun impacto_real_when_any_implementada() {
        val ideas = listOf(idea("i1", IdeaStatus.IMPLEMENTADA, guideline = "g"))
        val unlocked = BadgeEvaluator.evaluate(user, ideas)
        assertTrue(Badges.IMPACTO_REAL in unlocked)
    }

    @Test fun visionario_needs_three_distinct_guidelines() {
        val ideas = listOf(
            idea("i1", IdeaStatus.APROVADA, guideline = "g1"),
            idea("i2", IdeaStatus.APROVADA, guideline = "g2"),
            idea("i3", IdeaStatus.IMPLEMENTADA, guideline = "g3")
        )
        val unlocked = BadgeEvaluator.evaluate(user, ideas)
        assertTrue(Badges.VISIONARIO in unlocked)
    }

    @Test fun visionario_blocked_with_two_distinct() {
        val ideas = listOf(
            idea("i1", IdeaStatus.APROVADA, guideline = "g1"),
            idea("i2", IdeaStatus.APROVADA, guideline = "g2"),
            idea("i3", IdeaStatus.APROVADA, guideline = "g1")
        )
        val unlocked = BadgeEvaluator.evaluate(user, ideas)
        assertFalse(Badges.VISIONARIO in unlocked)
    }

    @Test fun does_not_re_unlock_existing_badges() {
        val withBadge = user.copy(badges = listOf(Badges.PRIMEIRA_IDEIA))
        val unlocked = BadgeEvaluator.evaluate(withBadge, listOf(idea("i1")))
        assertFalse(Badges.PRIMEIRA_IDEIA in unlocked)
    }
}
