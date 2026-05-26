package com.aguiabranca.app.core.domain

import com.aguiabranca.app.core.domain.error.Outcome
import com.aguiabranca.app.core.domain.model.Division
import com.aguiabranca.app.core.domain.model.Ice
import com.aguiabranca.app.core.domain.model.Idea
import kotlinx.coroutines.flow.Flow

data class CreateIdeaInput(
    val title: String,
    val description: String,
    val category: String,
    val division: Division,
    val guidelineId: String?,
    val authorId: String,
    val authorName: String
)

data class UpdateIdeaInput(
    val title: String,
    val description: String,
    val category: String,
    val division: Division,
    val guidelineId: String?
)

interface IdeasRepository {
    fun observeAll(): Flow<List<Idea>>
    fun observeByAuthor(authorId: String): Flow<List<Idea>>
    fun observeForCuration(): Flow<List<Idea>>
    fun observeByGuideline(guidelineId: String): Flow<List<Idea>>
    fun observe(id: String): Flow<Idea?>
    fun searchCategoriesByPrefix(prefix: String, limit: Int = 20): Flow<List<String>>

    suspend fun createIdea(input: CreateIdeaInput): Outcome<String>
    suspend fun updateIdea(id: String, input: UpdateIdeaInput): Outcome<Unit>
    suspend fun deleteIdea(id: String, authorId: String): Outcome<Unit>
    suspend fun saveIce(id: String, ice: Ice, reviewerId: String): Outcome<Unit>
    suspend fun rejectIdea(id: String, reviewerId: String, comment: String): Outcome<Unit>
}
