package com.aguiabranca.app.core.data.dto

import com.google.firebase.Timestamp
import com.google.firebase.firestore.PropertyName

data class UserDto(
    @get:PropertyName("name") @set:PropertyName("name")
    var name: String = "",
    @get:PropertyName("email") @set:PropertyName("email")
    var email: String = "",
    @get:PropertyName("role") @set:PropertyName("role")
    var role: String = "OPERADOR",
    @get:PropertyName("division") @set:PropertyName("division")
    var division: String = "CORPORATIVO",
    @get:PropertyName("points") @set:PropertyName("points")
    var points: Long = 0L,
    @get:PropertyName("badges") @set:PropertyName("badges")
    var badges: List<String> = emptyList(),
    @get:PropertyName("createdAt") @set:PropertyName("createdAt")
    var createdAt: Timestamp? = null
)
