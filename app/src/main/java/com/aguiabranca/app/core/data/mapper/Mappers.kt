package com.aguiabranca.app.core.data.mapper

import com.google.firebase.Timestamp
import com.aguiabranca.app.core.data.dto.GuidelineDto
import com.aguiabranca.app.core.data.dto.IdeaDto
import com.aguiabranca.app.core.data.dto.ProjectDto
import com.aguiabranca.app.core.data.dto.ProjectUpdateDto
import com.aguiabranca.app.core.data.dto.UserDto
import com.aguiabranca.app.core.domain.model.Division
import com.aguiabranca.app.core.domain.model.FieldChange
import com.aguiabranca.app.core.domain.model.Guideline
import com.aguiabranca.app.core.domain.model.Ice
import com.aguiabranca.app.core.domain.model.Idea
import com.aguiabranca.app.core.domain.model.IdeaStatus
import com.aguiabranca.app.core.domain.model.Pillar
import com.aguiabranca.app.core.domain.model.Project
import com.aguiabranca.app.core.domain.model.ProjectStage
import com.aguiabranca.app.core.domain.model.ProjectUpdate
import com.aguiabranca.app.core.domain.model.Role
import com.aguiabranca.app.core.domain.model.User

private fun Timestamp?.toMillis(): Long = this?.toDate()?.time ?: 0L
private fun Timestamp?.toMillisOrNull(): Long? = this?.toDate()?.time
private fun Long?.toTimestamp(): Timestamp? = this?.let { Timestamp(java.util.Date(it)) }

private inline fun <reified E : Enum<E>> safeValueOf(name: String, default: E): E =
    runCatching { enumValueOf<E>(name) }.getOrDefault(default)

fun UserDto.toDomain(id: String): User = User(
    id = id,
    name = name,
    email = email,
    role = safeValueOf(role, Role.OPERADOR),
    division = safeValueOf(division, Division.CORPORATIVO),
    points = points.toInt().coerceAtLeast(0),
    badges = badges,
    createdAt = createdAt.toMillis()
)

fun User.toDto(): UserDto = UserDto(
    name = name,
    email = email,
    role = role.name,
    division = division.name,
    points = points.toLong(),
    badges = badges,
    createdAt = createdAt.toTimestamp()
)

fun GuidelineDto.toDomain(id: String): Guideline = Guideline(
    id = id,
    title = title,
    description = description,
    pillar = safeValueOf(pillar, Pillar.DIRECIONAMENTO),
    authorId = authorId,
    authorName = authorName,
    createdAt = createdAt.toMillis(),
    updatedAt = updatedAt.toMillis()
)

fun Guideline.toDto(): GuidelineDto = GuidelineDto(
    title = title,
    description = description,
    pillar = pillar.name,
    authorId = authorId,
    authorName = authorName,
    createdAt = createdAt.toTimestamp(),
    updatedAt = updatedAt.toTimestamp()
)

fun IdeaDto.toDomain(id: String): Idea {
    val iceMap = ice
    val parsedIce: Ice? = if (iceMap != null) {
        val impact = (iceMap["impact"] as? Number)?.toInt() ?: 0
        val confidence = (iceMap["confidence"] as? Number)?.toInt() ?: 0
        val ease = (iceMap["ease"] as? Number)?.toInt() ?: 0
        if (impact > 0 && confidence > 0 && ease > 0) Ice(impact, confidence, ease) else null
    } else null

    return Idea(
        id = id,
        title = title,
        description = description,
        category = category,
        division = safeValueOf(division, Division.CORPORATIVO),
        guidelineId = guidelineId,
        authorId = authorId,
        authorName = authorName,
        status = safeValueOf(status, IdeaStatus.SUBMETIDA),
        ice = parsedIce,
        reviewerId = reviewerId,
        reviewComment = reviewComment,
        createdAt = createdAt.toMillis(),
        reviewedAt = reviewedAt.toMillisOrNull()
    )
}

fun Idea.toDto(): IdeaDto = IdeaDto(
    title = title,
    description = description,
    category = category,
    division = division.name,
    guidelineId = guidelineId,
    authorId = authorId,
    authorName = authorName,
    status = status.name,
    ice = ice?.let { mapOf("impact" to it.impact, "confidence" to it.confidence, "ease" to it.ease, "score" to it.score) },
    reviewerId = reviewerId,
    reviewComment = reviewComment,
    createdAt = createdAt.toTimestamp(),
    reviewedAt = reviewedAt.toTimestamp()
)

fun ProjectDto.toDomain(id: String): Project = Project(
    id = id,
    title = title,
    description = description,
    stage = safeValueOf(stage, ProjectStage.PLANEJAMENTO),
    statusText = statusText,
    investment = investment,
    targetDate = targetDate.toMillisOrNull(),
    financialReturn = financialReturn,
    productivityGain = productivityGain,
    costReduction = costReduction,
    division = safeValueOf(division, Division.CORPORATIVO),
    guidelineId = guidelineId,
    creatorManagerId = creatorManagerId,
    originatingIdeaId = originatingIdeaId,
    priorityScore = priorityScore?.toInt(),
    createdAt = createdAt.toMillis(),
    updatedAt = updatedAt.toMillis()
)

fun Project.toDto(): ProjectDto = ProjectDto(
    title = title,
    description = description,
    stage = stage.name,
    statusText = statusText,
    investment = investment,
    targetDate = targetDate.toTimestamp(),
    financialReturn = financialReturn,
    productivityGain = productivityGain,
    costReduction = costReduction,
    division = division.name,
    guidelineId = guidelineId,
    creatorManagerId = creatorManagerId,
    originatingIdeaId = originatingIdeaId,
    priorityScore = priorityScore?.toLong(),
    createdAt = createdAt.toTimestamp(),
    updatedAt = updatedAt.toTimestamp()
)

fun ProjectUpdateDto.toDomain(id: String): ProjectUpdate = ProjectUpdate(
    id = id,
    authorId = authorId,
    authorName = authorName,
    note = note,
    changes = changes.mapNotNull { raw ->
        @Suppress("UNCHECKED_CAST")
        val m = raw as? Map<String, Any?> ?: return@mapNotNull null
        FieldChange(
            field = m["field"] as? String ?: return@mapNotNull null,
            from = m["from"],
            to = m["to"]
        )
    },
    createdAt = createdAt.toMillis()
)
