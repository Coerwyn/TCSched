<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { api, type EventDto, type Registration, type Store } from '../../api'
import QrCode from '../../components/QrCode.vue'
import SeatsBar from '../../components/SeatsBar.vue'
import { publicBaseUrl } from '../../config'
import { errorMessage } from '../../lib/errors'
import { formatRange, formatTime } from '../../lib/time'

const props = defineProps<{ id: string }>()

const ev = ref<EventDto>()
const store = ref<Store>()
const registrations = ref<Registration[]>([])
const error = ref<string>()
const copied = ref(false)

const registrationUrl = computed(() => `${publicBaseUrl}/e/${props.id}`)

async function load() {
  try {
    const [s, e, r] = await Promise.all([api.store(), api.event(props.id), api.registrations(props.id)])
    store.value = s
    ev.value = e
    registrations.value = r
    error.value = undefined
  } catch (e) {
    error.value = errorMessage(e)
  }
}

// Refresh every few seconds so the organizer sees players register in real time.
let timer: ReturnType<typeof setInterval> | undefined
onMounted(() => {
  load()
  timer = setInterval(load, 5000)
})
onUnmounted(() => clearInterval(timer))

async function copyLink() {
  try {
    await navigator.clipboard.writeText(registrationUrl.value)
    copied.value = true
    setTimeout(() => (copied.value = false), 2000)
  } catch {
    // Clipboard needs a secure context (https or localhost); the link is visible to copy by hand.
  }
}
</script>

<template>
  <p v-if="error && !ev" class="error">{{ error }}</p>

  <template v-if="ev && store">
    <RouterLink to="/organizer" class="small">← Calendar</RouterLink>
    <h1>{{ ev.name }}</h1>
    <p class="muted">{{ ev.gameName }} · {{ ev.format }}</p>
    <p>
      {{ formatRange(ev.startsAtUtc, ev.endsAtUtc, store.timeZone) }}<br />
      <span class="muted">{{ store.name }}, {{ store.address }}</span>
    </p>
    <p><a class="button secondary" :href="api.icsUrl(ev.id)" download>Add to calendar (.ics)</a></p>

    <div class="grid">
      <section class="card">
        <h2>Registration</h2>
        <SeatsBar :registered="ev.registeredCount" :capacity="ev.capacity" :min-players="ev.minPlayers" />

        <h3>Players</h3>
        <ol v-if="registrations.length" class="players">
          <li v-for="r in registrations" :key="r.id">
            {{ r.displayName }} <span class="muted small">{{ formatTime(r.registeredAtUtc, store.timeZone) }}</span>
          </li>
        </ol>
        <p v-else class="muted">No one yet. Share the QR code.</p>
        <p v-if="error" class="error small">Refresh failed: {{ error }}</p>
      </section>

      <section class="card qr">
        <h2>Scan to register</h2>
        <QrCode :value="registrationUrl" :size="220" />
        <p class="link small"><a :href="registrationUrl" target="_blank" rel="noopener">{{ registrationUrl }}</a></p>
        <button type="button" class="secondary" @click="copyLink">{{ copied ? 'Copied!' : 'Copy link' }}</button>
      </section>
    </div>
  </template>
</template>
