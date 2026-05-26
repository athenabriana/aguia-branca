package com.aguiabranca.app.core.domain.model

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class IceTest {

    @Test fun score_is_product_of_three_dimensions() {
        val ice = Ice(impact = 8, confidence = 7, ease = 6)
        assertEquals(8 * 7 * 6, ice.score)
    }

    @Test fun isComplete_true_when_all_within_range() {
        val ice = Ice(impact = 1, confidence = 10, ease = 5)
        assertTrue(ice.isComplete)
    }

    @Test fun isComplete_false_when_any_is_zero() {
        assertFalse(Ice(0, 5, 5).isComplete)
        assertFalse(Ice(5, 0, 5).isComplete)
        assertFalse(Ice(5, 5, 0).isComplete)
    }

    @Test fun isComplete_false_when_any_above_ten() {
        assertFalse(Ice(11, 5, 5).isComplete)
        assertFalse(Ice(5, 11, 5).isComplete)
        assertFalse(Ice(5, 5, 11).isComplete)
    }

    @Test fun isComplete_true_at_lower_bound() {
        assertTrue(Ice(1, 1, 1).isComplete)
    }

    @Test fun isComplete_true_at_upper_bound() {
        assertTrue(Ice(10, 10, 10).isComplete)
        assertEquals(1000, Ice(10, 10, 10).score)
    }
}
