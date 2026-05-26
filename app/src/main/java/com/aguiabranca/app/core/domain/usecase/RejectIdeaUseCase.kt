package com.aguiabranca.app.core.domain.usecase

import com.aguiabranca.app.core.domain.IdeasRepository
import com.aguiabranca.app.core.domain.error.Outcome
import javax.inject.Inject

class RejectIdeaUseCase @Inject constructor(
    private val ideasRepository: IdeasRepository
) {
    suspend operator fun invoke(ideaId: String, reviewerId: String, comment: String): Outcome<Unit> =
        ideasRepository.rejectIdea(ideaId, reviewerId, comment)
}
