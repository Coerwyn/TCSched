<script setup lang="ts">
import { computed } from 'vue'

const props = defineProps<{ registered: number; capacity: number; minPlayers: number }>()

const percent = computed(() => Math.round((props.registered / props.capacity) * 100))
const needed = computed(() => Math.max(0, props.minPlayers - props.registered))
</script>

<template>
  <div class="seats">
    <div class="seats-label">
      <strong>{{ registered }} / {{ capacity }}</strong> registered
      <span v-if="registered >= capacity" class="badge full">Full</span>
      <span v-else class="muted">· {{ capacity - registered }} seats left</span>
    </div>
    <div class="bar" role="progressbar" :aria-valuenow="registered" aria-valuemin="0" :aria-valuemax="capacity">
      <div class="bar-fill" :class="{ full: registered >= capacity }" :style="{ width: `${percent}%` }" />
    </div>
    <div v-if="needed > 0" class="muted small">Needs {{ needed }} more to run (minimum {{ minPlayers }}).</div>
  </div>
</template>
