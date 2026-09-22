package com.aguiabranca.app.core.network

import com.aguiabranca.app.core.domain.ProjectInput
import com.aguiabranca.app.core.domain.model.Division
import com.aguiabranca.app.core.domain.model.IdeaStatus
import com.aguiabranca.app.core.domain.model.Pillar
import com.aguiabranca.app.core.domain.model.ProjectStage
import com.aguiabranca.app.core.domain.model.Role
import com.aguiabranca.app.core.network.dto.GuidelineDto
import com.aguiabranca.app.core.network.dto.IdeaDto
import com.aguiabranca.app.core.network.dto.ProjectDto
import com.aguiabranca.app.core.network.dto.ProjectRequestDto
import com.aguiabranca.app.core.network.dto.ProjectUpdateDto
import com.aguiabranca.app.core.network.dto.UserProfileDto
import com.aguiabranca.app.core.network.mapper.Iso
import com.aguiabranca.app.core.network.mapper.toDomain
import kotlinx.serialization.encodeToString
import kotlinx.serialization.json.Json
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

/** Round-trip JSON da API → DTO → domínio, por entidade, incluindo os `null` de prazo, ICE, orientação e campanha. */
class NetMappersTest {

    @Test fun `user profile keeps role, division, points and the badges the server persisted`() {
        val u = TestJson.decodeFromString<UserProfileDto>(
            """{"id":"u1","name":"Ana","email":"a@x.com","role":"LIDER","division":"LOGISTICA","points":120,"badges":["Primeira Ideia"]}"""
        ).toDomain()
        assertEquals(Role.LIDER, u.role); assertEquals(Division.LOGISTICA, u.division)
        assertEquals(120, u.points); assertEquals(listOf("Primeira Ideia"), u.badges)
    }

    @Test fun `guideline round trip with and without campaign`() {
        val json = """{"id":"g1","title":"T","description":"D","pillar":"MENSURACAO","campaign":"Camp","authorId":"a","authorName":"L","createdAt":"2026-09-01T00:00:00Z","updatedAt":"2026-09-02T00:00:00Z"}"""
        val g = TestJson.decodeFromString<GuidelineDto>(json).toDomain()
        assertEquals(Pillar.MENSURACAO, g.pillar); assertEquals("Camp", g.campaign)
        assertEquals(Iso.parse("2026-09-02T00:00:00Z"), g.updatedAt)
        assertNull(TestJson.decodeFromString<GuidelineDto>(json.replace("\"campaign\":\"Camp\",", "")).toDomain().campaign)
    }

    @Test fun `idea with null ICE, guideline and linked project`() {
        val i = TestJson.decodeFromString<IdeaDto>(
            """{"id":"i1","title":"T","description":"D","category":"Tec","division":"COMERCIO","guidelineId":null,"guidelineTitle":null,"authorId":"a","authorName":"Ana",
                "status":"EM_ANALISE","ice":null,"reviewerId":null,"reviewComment":null,"createdAt":"2026-09-01T10:00:00Z","updatedAt":"2026-09-01T10:00:00Z","reviewedAt":null,"linkedProject":null,"pointsAwarded":null}"""
        ).toDomain()
        assertEquals(IdeaStatus.EM_ANALISE, i.status)
        assertNull(i.ice); assertNull(i.guidelineId); assertNull(i.linkedProject); assertNull(i.reviewedAt)
    }

    @Test fun `idea with ICE and linked project keeps every value`() {
        val i = TestJson.decodeFromString<IdeaDto>(
            """{"id":"i1","title":"T","division":"LOGISTICA","guidelineId":"g","guidelineTitle":"G","authorId":"a","authorName":"Ana","status":"IMPLEMENTADA",
                "ice":{"impact":9,"confidence":9,"ease":8,"score":648},"reviewedAt":"2026-09-03T10:00:00Z","linkedProject":{"id":"p","stage":"CONCLUIDO","updatedAt":"2026-09-10T00:00:00Z"}}"""
        ).toDomain()
        assertEquals(648, i.ice?.score); assertEquals(ProjectStage.CONCLUIDO, i.linkedProject?.stage); assertTrue(i.reviewedAt!! > 0)
    }

    @Test fun `project with null deadline, guideline and origin`() {
        val p = TestJson.decodeFromString<ProjectDto>(
            """{"id":"p1","title":"T","stage":"PLANEJAMENTO","division":"CORPORATIVO","targetDate":null,"guidelineId":null,"originatingIdeaId":null,"priorityScore":null,"version":1}"""
        ).toDomain()
        assertNull(p.targetDate); assertNull(p.guidelineId); assertNull(p.originatingIdeaId); assertNull(p.priorityScore)
        assertEquals(0.0, p.investment, 0.0)
    }

    @Test fun `project request encodes the form back to the contract with ISO deadline and no server-owned fields`() {
        val input = ProjectInput("T", "D", ProjectStage.EM_EXECUCAO, "s", 1000.0, Iso.parse("2026-12-20T00:00:00Z"), 2000.0, 5.0, 300.0, Division.LOGISTICA, null, null, null, 4)
        val req = ProjectRequestDto(input.title, input.description, input.stage.name, input.statusText, input.investment, input.targetDate?.let(Iso::format),
            input.financialReturn, input.productivityGain, input.costReduction, input.division.name, input.guidelineId, input.responsibleId, null, input.version)

        val json = TestJson.encodeToString(req)

        assertTrue(json.contains("\"targetDate\":\"2026-12-20T00:00:00Z\"") && json.contains("\"version\":4"))
        assertFalse(json.contains("guidelineId")); assertFalse(json.contains("creatorManagerId"))   // nulos/ownership não vão
    }

    @Test fun `field changes convert numbers, texts, nulls and the deadline date`() {
        val u = TestJson.decodeFromString<ProjectUpdateDto>(
            """{"id":"u","authorId":"a","authorName":"G","note":"n","createdAt":"2026-09-05T10:00:00Z","changes":[
                {"field":"investment","from":100,"to":250.5},{"field":"title","from":"A","to":"B"},{"field":"targetDate","from":null,"to":"2026-12-20T00:00:00Z"},{"field":"guidelineId","from":"g1","to":null}]}"""
        ).toDomain()
        val (investment, title, target, guideline) = u.changes
        assertEquals(100.0, investment.from as Double, 0.0); assertEquals(250.5, investment.to as Double, 0.0)
        assertEquals("B", title.to)
        assertNull(target.from); assertEquals(1_797_724_800_000L, target.to)
        assertEquals("g1", guideline.from); assertNull(guideline.to)
    }

    @Test fun `unknown enum values fall back to safe defaults instead of crashing`() {
        val p = TestJson.decodeFromString<ProjectDto>("""{"id":"p","title":"T","stage":"NOVO_ESTAGIO","division":"MARTE"}""").toDomain()
        assertEquals(ProjectStage.PLANEJAMENTO, p.stage); assertEquals(Division.CORPORATIVO, p.division)
    }

    @Test fun `ISO dates accept fractions and round trip through format`() {
        assertEquals(1_797_724_800_000L, Iso.parse("2026-12-20T00:00:00Z"))
        assertEquals(1_797_724_800_470L, Iso.parse("2026-12-20T00:00:00.47Z"))
        assertEquals(1_797_724_800_123L, Iso.parse("2026-12-20T00:00:00.1234567Z"))
        assertEquals("2026-12-20T00:00:00Z", Iso.format(1_797_724_800_000L))
        assertNull(Iso.parse(null)); assertNull(Iso.parse("ontem"))
    }
}
