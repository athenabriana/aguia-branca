package com.aguiabranca.app.core.data.mapper

import com.aguiabranca.app.core.domain.model.Division
import com.aguiabranca.app.core.domain.model.Guideline
import com.aguiabranca.app.core.domain.model.Ice
import com.aguiabranca.app.core.domain.model.Idea
import com.aguiabranca.app.core.domain.model.IdeaStatus
import com.aguiabranca.app.core.domain.model.Pillar
import com.aguiabranca.app.core.domain.model.Project
import com.aguiabranca.app.core.domain.model.ProjectStage
import com.aguiabranca.app.core.domain.model.Role
import com.aguiabranca.app.core.domain.model.User
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

class MapperTest {

    @Test fun user_roundtrip_preserves_fields() {
        val u = User("uid", "Ana", "ana@x.com", Role.LIDER, Division.LOGISTICA, 120, listOf("Primeira Ideia"), 1700000000000L)
        val back = u.toDto().toDomain("uid")
        assertEquals(u, back)
    }

    @Test fun guideline_roundtrip_preserves_fields() {
        val g = Guideline("gid", "Reduzir custos", "Descrição", Pillar.MENSURACAO, "uid", "Líder", 1L, 2L)
        val back = g.toDto().toDomain("gid")
        assertEquals(g, back)
    }

    @Test fun idea_roundtrip_with_ice_preserves_score() {
        val idea = Idea(
            id = "iid",
            title = "Otimizar rota",
            description = "Descrição",
            category = "Logística",
            division = Division.LOGISTICA,
            guidelineId = "g1",
            authorId = "u1",
            authorName = "Op",
            status = IdeaStatus.EM_ANALISE,
            ice = Ice(8, 7, 6),
            reviewerId = "g1",
            reviewComment = null,
            createdAt = 1000L,
            reviewedAt = 2000L
        )
        val back = idea.toDto().toDomain("iid")
        assertEquals(idea, back)
        assertEquals(8 * 7 * 6, back.ice!!.score)
    }

    @Test fun idea_roundtrip_with_null_ice() {
        val idea = Idea(
            id = "iid",
            title = "T",
            description = "D",
            category = "C",
            division = Division.CORPORATIVO,
            guidelineId = null,
            authorId = "u",
            authorName = "n",
            status = IdeaStatus.SUBMETIDA,
            ice = null,
            reviewerId = null,
            reviewComment = null,
            createdAt = 0L,
            reviewedAt = null
        )
        val back = idea.toDto().toDomain("iid")
        assertNull(back.ice)
        assertEquals(idea, back)
    }

    @Test fun project_roundtrip_preserves_financials() {
        val p = Project(
            id = "pid",
            title = "Projeto X",
            description = "D",
            stage = ProjectStage.EM_EXECUCAO,
            statusText = "rolando",
            investment = 100_000.0,
            targetDate = 1800000000000L,
            financialReturn = 150_000.0,
            productivityGain = 12.5,
            costReduction = 25_000.0,
            division = Division.COMERCIO,
            guidelineId = "g",
            creatorManagerId = "ger",
            originatingIdeaId = "iid",
            createdAt = 1L,
            updatedAt = 2L
        )
        val back = p.toDto().toDomain("pid")
        assertEquals(p, back)
    }
}
