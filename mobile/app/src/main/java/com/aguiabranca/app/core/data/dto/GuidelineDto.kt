package com.aguiabranca.app.core.data.dto

import com.google.firebase.Timestamp
import com.google.firebase.firestore.PropertyName

data class GuidelineDto(
    @get:PropertyName("title") @set:PropertyName("title")
    var title: String = "",
    @get:PropertyName("description") @set:PropertyName("description")
    var description: String = "",
    @get:PropertyName("pillar") @set:PropertyName("pillar")
    var pillar: String = "DIRECIONAMENTO",
    @get:PropertyName("authorId") @set:PropertyName("authorId")
    var authorId: String = "",
    @get:PropertyName("authorName") @set:PropertyName("authorName")
    var authorName: String = "",
    @get:PropertyName("createdAt") @set:PropertyName("createdAt")
    var createdAt: Timestamp? = null,
    @get:PropertyName("updatedAt") @set:PropertyName("updatedAt")
    var updatedAt: Timestamp? = null
)
