package com.aguiabranca.app.bootstrap

import com.google.firebase.Timestamp
import com.google.firebase.auth.FirebaseAuth
import com.google.firebase.firestore.FirebaseFirestore
import com.aguiabranca.app.core.data.dto.GuidelineDto
import com.aguiabranca.app.core.data.dto.IdeaDto
import com.aguiabranca.app.core.data.dto.ProjectDto
import com.aguiabranca.app.core.data.dto.UserDto
import com.aguiabranca.app.core.domain.model.Division
import com.aguiabranca.app.core.domain.model.IdeaStatus
import com.aguiabranca.app.core.domain.model.Pillar
import com.aguiabranca.app.core.domain.model.ProjectStage
import com.aguiabranca.app.core.domain.model.Role
import kotlinx.coroutines.tasks.await
import javax.inject.Inject
import javax.inject.Singleton

@Singleton
class SeedData @Inject constructor(
    private val auth: FirebaseAuth,
    private val firestore: FirebaseFirestore
) {

    data class DemoUser(val email: String, val password: String, val name: String, val role: Role, val division: Division)

    private val demoUsers = listOf(
        DemoUser("lider@aguiabranca.com", "aguiabranca123", "Marina Costa", Role.LIDER, Division.CORPORATIVO),
        DemoUser("gestor@aguiabranca.com", "aguiabranca123", "Carlos Almeida", Role.GESTOR, Division.LOGISTICA),
        DemoUser("operador@aguiabranca.com", "aguiabranca123", "Bruna Lima", Role.OPERADOR, Division.LOGISTICA)
    )

    suspend fun seed() {
        val uids = mutableMapOf<String, String>()
        for (du in demoUsers) {
            val methods = auth.fetchSignInMethodsForEmail(du.email).await().signInMethods.orEmpty()
            val uid = if (methods.isEmpty()) {
                val res = auth.createUserWithEmailAndPassword(du.email, du.password).await()
                res.user!!.uid
            } else {
                val res = auth.signInWithEmailAndPassword(du.email, du.password).await()
                res.user!!.uid
            }
            uids[du.email] = uid
            val ref = firestore.collection("users").document(uid)
            val existing = ref.get().await()
            if (!existing.exists()) {
                ref.set(
                    UserDto(
                        name = du.name,
                        email = du.email,
                        role = du.role.name,
                        division = du.division.name,
                        points = 0L,
                        badges = emptyList(),
                        createdAt = Timestamp.now()
                    )
                ).await()
            }
        }
        auth.signOut()

        val guidelinesCol = firestore.collection("strategicGuidelines")
        if (guidelinesCol.limit(1).get().await().isEmpty) {
            val liderUid = uids.getValue("lider@aguiabranca.com")
            val liderName = "Marina Costa"
            val baseTime = Timestamp.now()
            val guidelines = listOf(
                GuidelineDto("Reduzir custo logístico em 15%", "Otimizar rotas, frota e parcerias de transporte para reduzir CPV.", Pillar.MENSURACAO.name, liderUid, liderName, baseTime, baseTime),
                GuidelineDto("Digitalizar o atendimento ao cliente", "Criar canais digitais e self-service para clientes do segmento Passageiros.", Pillar.DIRECIONAMENTO.name, liderUid, liderName, baseTime, baseTime),
                GuidelineDto("Lançar 3 projetos piloto de inovação aberta", "Provas de conceito com startups e universidades em 2026.", Pillar.PROJETOS.name, liderUid, liderName, baseTime, baseTime),
                GuidelineDto("Aumentar engajamento operacional", "Programa de captura de ideias da base operacional.", Pillar.IDEIAS.name, liderUid, liderName, baseTime, baseTime)
            )
            val guidelineIds = guidelines.map { guidelinesCol.add(it).await().id }

            val operadorUid = uids.getValue("operador@aguiabranca.com")
            val operadorName = "Bruna Lima"
            val gestorUid = uids.getValue("gestor@aguiabranca.com")
            val gestorName = "Carlos Almeida"
            val ideasCol = firestore.collection("ideas")
            val ideas = listOf(
                IdeaDto("Roteirização inteligente da frota leve", "Usar telemetria para reordenar entregas urbanas.", "Operações", Division.LOGISTICA.name, guidelineIds[0], operadorUid, operadorName, IdeaStatus.IMPLEMENTADA.name, mapOf("impact" to 9, "confidence" to 8, "ease" to 7, "score" to 504), gestorUid, null, baseTime, baseTime),
                IdeaDto("Self-service de remarcação", "Permitir remarcação de passagem 100% no app.", "Atendimento", Division.PASSAGEIROS.name, guidelineIds[1], operadorUid, operadorName, IdeaStatus.APROVADA.name, mapOf("impact" to 8, "confidence" to 7, "ease" to 6, "score" to 336), gestorUid, null, baseTime, baseTime),
                IdeaDto("Hub de inovação com universidades", "Programa estruturado de PoCs.", "Inovação Aberta", Division.CORPORATIVO.name, guidelineIds[2], operadorUid, operadorName, IdeaStatus.EM_ANALISE.name, mapOf("impact" to 7, "confidence" to 6, "ease" to 5, "score" to 210), gestorUid, null, baseTime, null),
                IdeaDto("Mural digital de ideias por unidade", "Gameficar captura de ideias por unidade operacional.", "Engajamento", Division.LOGISTICA.name, guidelineIds[3], operadorUid, operadorName, IdeaStatus.SUBMETIDA.name, null, null, null, baseTime, null),
                IdeaDto("Comparar consumo de combustível por motorista", "Análise mensal por divisão.", "Operações", Division.LOGISTICA.name, guidelineIds[0], operadorUid, operadorName, IdeaStatus.SUBMETIDA.name, null, null, null, baseTime, null),
                IdeaDto("Pesquisa NPS automatizada", "NPS por SMS no fim de viagem.", "Atendimento", Division.PASSAGEIROS.name, guidelineIds[1], operadorUid, operadorName, IdeaStatus.REJEITADA.name, null, gestorUid, "Já temos pesquisa via app — duplicidade", baseTime, baseTime)
            )
            val ideaIds = ideas.map { ideasCol.add(it).await().id }

            val projectsCol = firestore.collection("projects")
            val p1 = ProjectDto("PROJ: Roteirização inteligente", "Implementar pilot de algoritmo de roteirização.", ProjectStage.CONCLUIDO.name, "Piloto encerrado com sucesso", 120_000.0, baseTime, 180_000.0, 12.0, 30_000.0, Division.LOGISTICA.name, guidelineIds[0], gestorUid, ideaIds[0], baseTime, baseTime)
            val p2 = ProjectDto("PROJ: Self-service remarcação", "Implementação no aplicativo de passageiros.", ProjectStage.EM_EXECUCAO.name, "MVP em homologação", 80_000.0, baseTime, 0.0, 0.0, 0.0, Division.PASSAGEIROS.name, guidelineIds[1], gestorUid, ideaIds[1], baseTime, baseTime)
            val p3 = ProjectDto("Modernização do data lake", "Migração para arquitetura unificada.", ProjectStage.PLANEJAMENTO.name, "Fase de descoberta", 250_000.0, baseTime, 0.0, 0.0, 0.0, Division.CORPORATIVO.name, guidelineIds[3], gestorUid, null, baseTime, baseTime)
            listOf(p1, p2, p3).forEach { dto ->
                val ref = projectsCol.add(dto).await()
                ref.collection("updates").add(
                    mapOf(
                        "authorId" to gestorUid,
                        "authorName" to gestorName,
                        "note" to "Projeto criado (seed)",
                        "changes" to emptyList<Map<String, Any?>>(),
                        "createdAt" to Timestamp.now()
                    )
                ).await()
            }
        }
    }
}
