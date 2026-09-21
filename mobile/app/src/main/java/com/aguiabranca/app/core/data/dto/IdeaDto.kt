package com.aguiabranca.app.core.data.dto

import com.google.firebase.Timestamp
import com.google.firebase.firestore.PropertyName

data class IdeaDto(
    @get:PropertyName("title") @set:PropertyName("title")
    var title: String = "",
    @get:PropertyName("description") @set:PropertyName("description")
    var description: String = "",
    @get:PropertyName("category") @set:PropertyName("category")
    var category: String = "",
    @get:PropertyName("division") @set:PropertyName("division")
    var division: String = "CORPORATIVO",
    @get:PropertyName("guidelineId") @set:PropertyName("guidelineId")
    var guidelineId: String? = null,
    @get:PropertyName("authorId") @set:PropertyName("authorId")
    var authorId: String = "",
    @get:PropertyName("authorName") @set:PropertyName("authorName")
    var authorName: String = "",
    @get:PropertyName("status") @set:PropertyName("status")
    var status: String = "SUBMETIDA",
    @get:PropertyName("ice") @set:PropertyName("ice")
    var ice: Map<String, Any>? = null,
    @get:PropertyName("reviewerId") @set:PropertyName("reviewerId")
    var reviewerId: String? = null,
    @get:PropertyName("reviewComment") @set:PropertyName("reviewComment")
    var reviewComment: String? = null,
    @get:PropertyName("createdAt") @set:PropertyName("createdAt")
    var createdAt: Timestamp? = null,
    @get:PropertyName("reviewedAt") @set:PropertyName("reviewedAt")
    var reviewedAt: Timestamp? = null
)
