<script setup lang="ts">
import { computed } from 'vue'
import { useRoute } from 'vue-router'

const route = useRoute()
const isOrganizer = computed(() => route.path.startsWith('/organizer'))
</script>

<template>
  <header v-if="isOrganizer" class="nav">
    <nav class="container">
      <RouterLink to="/organizer">Calendar</RouterLink>
      <RouterLink to="/organizer/events/new">New event</RouterLink>
    </nav>
  </header>
  <main class="container">
    <RouterView />
  </main>
</template>
