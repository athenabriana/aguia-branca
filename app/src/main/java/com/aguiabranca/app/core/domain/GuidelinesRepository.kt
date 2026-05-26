package com.aguiabranca.app.core.domain

import com.aguiabranca.app.core.domain.error.Outcome
import com.aguiabranca.app.core.domain.model.Guideline
import com.aguiabranca.app.core.domain.model.Pillar
import kotlinx.coroutines.flow.Flow

interface GuidelinesRepository {
    fun observeAll(): Flow<List<Guideline>>
    fun observe(id: String): Flow<Guideline?>
    suspend fun create(title: String, description: String, pillar: Pillar, authorId: String, authorName: String): Outcome<String>
    suspend fun update(id: String, title: String, description: String, pillar: Pillar): Outcome<Unit>
    suspend fun delete(id: String): Outcome<Unit>
}
