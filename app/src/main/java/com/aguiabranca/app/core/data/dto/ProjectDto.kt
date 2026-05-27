package com.aguiabranca.app.core.data.dto

import com.google.firebase.Timestamp
import com.google.firebase.firestore.PropertyName

data class ProjectDto(
    @get:PropertyName("title") @set:PropertyName("title")
    var title: String = "",
    @get:PropertyName("description") @set:PropertyName("description")
    var description: String = "",
    @get:PropertyName("stage") @set:PropertyName("stage")
    var stage: String = "PLANEJAMENTO",
    @get:PropertyName("statusText") @set:PropertyName("statusText")
    var statusText: String = "",
    @get:PropertyName("investment") @set:PropertyName("investment")
    var investment: Double = 0.0,
    @get:PropertyName("targetDate") @set:PropertyName("targetDate")
    var targetDate: Timestamp? = null,
    @get:PropertyName("financialReturn") @set:PropertyName("financialReturn")
    var financialReturn: Double = 0.0,
    @get:PropertyName("productivityGain") @set:PropertyName("productivityGain")
    var productivityGain: Double = 0.0,
    @get:PropertyName("costReduction") @set:PropertyName("costReduction")
    var costReduction: Double = 0.0,
    @get:PropertyName("division") @set:PropertyName("division")
    var division: String = "CORPORATIVO",
    @get:PropertyName("guidelineId") @set:PropertyName("guidelineId")
    var guidelineId: String? = null,
    @get:PropertyName("creatorManagerId") @set:PropertyName("creatorManagerId")
    var creatorManagerId: String = "",
    @get:PropertyName("originatingIdeaId") @set:PropertyName("originatingIdeaId")
    var originatingIdeaId: String? = null,
    @get:PropertyName("priorityScore") @set:PropertyName("priorityScore")
    var priorityScore: Long? = null,
    @get:PropertyName("reporterId") @set:PropertyName("reporterId")
    var reporterId: String? = null,
    @get:PropertyName("reporterName") @set:PropertyName("reporterName")
    var reporterName: String? = null,
    @get:PropertyName("responsibleId") @set:PropertyName("responsibleId")
    var responsibleId: String? = null,
    @get:PropertyName("responsibleName") @set:PropertyName("responsibleName")
    var responsibleName: String? = null,
    @get:PropertyName("createdAt") @set:PropertyName("createdAt")
    var createdAt: Timestamp? = null,
    @get:PropertyName("updatedAt") @set:PropertyName("updatedAt")
    var updatedAt: Timestamp? = null
)

data class ProjectUpdateDto(
    @get:PropertyName("authorId") @set:PropertyName("authorId")
    var authorId: String = "",
    @get:PropertyName("authorName") @set:PropertyName("authorName")
    var authorName: String = "",
    @get:PropertyName("note") @set:PropertyName("note")
    var note: String = "",
    @get:PropertyName("changes") @set:PropertyName("changes")
    var changes: List<Map<String, Any?>> = emptyList(),
    @get:PropertyName("createdAt") @set:PropertyName("createdAt")
    var createdAt: Timestamp? = null
)
