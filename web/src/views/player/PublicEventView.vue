<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { api, ApiError, type EventDto, type RegistrationResult, type Store } from '../../api'
import SeatsBar from '../../components/SeatsBar.vue'
import { errorMessage, fieldError } from '../../lib/errors'
import { formatRange } from '../../lib/time'

const props = defineProps<{ id: string }>()

const ev = ref<EventDto>()
const store = ref<Store>()
const loadError = ref<string>()

const name = ref('')
const submitting = ref(false)
const result = ref<RegistrationResult>()
const submitError = ref<unknown>()

const started = computed(() => !!ev.value && new Date(ev.value.startsAtUtc) <= new Date())
const closedReason = computed(() =>
  started.value ? 'Registration is closed: this event has already started.'
    : ev.value?.isFull ? 'Sorry, this event is full.' : undefined)
const nameError = computed(() => fieldError(submitError.value, 'name'))
const generalError = computed(() => (submitError.value && !nameError.value ? errorMessage(submitError.value) : undefined))
const isDuplicate = computed(() => submitError.value instanceof ApiError && submitError.value.code === 'DUPLICATE_REGISTRATION')

async function load() {
  try {
    const [s, e] = await Promise.all([api.store(), api.event(props.id)])
    store.value = s
    ev.value = e
  } catch (e) {
    loadError.value = errorMessage(e)
  }
}
onMounted(load)

async function register() {
  submitting.value = true
  submitError.value = undefined
  try {
    result.value = await api.register(props.id, name.value)
  } catch (e) {
    submitError.value = e
  } finally {
    submitting.value = false
    load() // show the latest seat count whether it worked or not
  }
}
</script>

<template>
  <p v-if="loadError" class="error">{{ loadError }}</p>

  <article v-if="ev && store" class="public">
    <h1>{{ ev.name }}</h1>
    <p class="muted">{{ ev.gameName }} · {{ ev.format }}</p>
    <p>
      {{ formatRange(ev.startsAtUtc, ev.endsAtUtc, store.timeZone) }}<br />
      {{ store.name }}<br />
      <span class="muted">{{ store.address }}</span>
    </p>
    <SeatsBar :registered="ev.registeredCount" :capacity="ev.capacity" :min-players="ev.minPlayers" />

    <section v-if="result" class="card success" role="status">
      <h2>You're in, {{ result.displayName }}!</h2>
      <p>You have seat {{ result.registeredCount }} of {{ result.capacity }}. See you there.</p>
      <a class="button secondary" :href="api.icsUrl(ev.id)" download>Add to my calendar</a>
    </section>

    <p v-else-if="closedReason && !submitError" class="card notice" role="status">{{ closedReason }}</p>

    <form v-else class="form card" novalidate @submit.prevent="register">
      <h2>Register</h2>
      <label>
        Your name
        <input v-model="name" required maxlength="60" autocomplete="name" placeholder="First and last name" />
        <span v-if="nameError" class="error">{{ nameError }}</span>
      </label>
      <p v-if="generalError" class="error" role="alert">{{ generalError }}</p>
      <button type="submit" :disabled="submitting || (!!closedReason && !isDuplicate)">
        {{ submitting ? 'Registering…' : 'Register' }}
      </button>
    </form>
  </article>
</template>
