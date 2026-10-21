<script setup lang="ts">
import { computed, onMounted, onUnmounted, reactive, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { api, ApiError, type GameTemplate, type Store } from '../../api'
import { errorMessage, fieldError } from '../../lib/errors'
import { formatTime, storeToday, wallTimePlus, zoneName } from '../../lib/time'

const MAX_CAPACITY = 30 // global limit; templates may set a lower one

const route = useRoute()
const router = useRouter()

const templates = ref<GameTemplate[]>([])
const store = ref<Store>()
const loadError = ref<string>()
const submitError = ref<unknown>()
const submitting = ref(false)

const form = reactive({
  name: '',
  gameTemplateId: '',
  format: '',
  date: typeof route.query.date === 'string' ? route.query.date : '',
  time: '18:00',
  capacity: 0,
})

const template = computed(() => templates.value.find((t) => t.id === form.gameTemplateId))
const maxCapacity = computed(() => Math.min(template.value?.maxCapacity ?? MAX_CAPACITY, MAX_CAPACITY))
const startsAtLocal = computed(() => (form.date && form.time ? `${form.date}T${form.time}` : ''))
const endsAt = computed(() =>
  template.value && startsAtLocal.value ? wallTimePlus(startsAtLocal.value, template.value.defaultDurationMinutes) : '')

// Times are entered on the store's clock, which may differ from the browser's. Show the
// store's zone and current time so "6:00 PM" is unambiguous; refresh it every 30 s.
const now = ref(new Date())
const clockTimer = setInterval(() => (now.value = new Date()), 30_000)
onUnmounted(() => clearInterval(clockTimer))
const storeZone = computed(() => (store.value ? zoneName(store.value.timeZone, now.value) : ''))
const storeZoneLong = computed(() => (store.value ? zoneName(store.value.timeZone, now.value, 'long') : ''))
const storeNow = computed(() => (store.value ? formatTime(now.value.toISOString(), store.value.timeZone) : ''))
const browserDiffers = computed(() =>
  !!store.value && Intl.DateTimeFormat().resolvedOptions().timeZone !== store.value.timeZone)

// Picking a game applies its template defaults. All values come from the template data.
watch(template, (t) => {
  if (!t) return
  form.format = t.defaultFormat
  form.capacity = t.defaultCapacity
})

onMounted(async () => {
  try {
    const [s, t] = await Promise.all([api.store(), api.templates()])
    store.value = s
    templates.value = t
    form.date ||= storeToday(s.timeZone)
    // The store's configured default game, else the first one.
    form.gameTemplateId = t.find((x) => x.id === s.defaultGameTemplateId)?.id ?? t[0]?.id ?? ''
  } catch (e) {
    loadError.value = errorMessage(e)
  }
})

const err = (field: string) => fieldError(submitError.value, field)

/** Errors that aren't tied to a field (e.g. unknown game, network) show under the form. */
const generalError = computed(() => {
  const e = submitError.value
  if (!e) return undefined
  return e instanceof ApiError && Object.keys(e.errors).length > 0 ? undefined : errorMessage(e)
})

async function submit() {
  submitting.value = true
  submitError.value = undefined
  try {
    const ev = await api.createEvent({
      name: form.name,
      gameTemplateId: form.gameTemplateId,
      format: form.format,
      startsAtLocal: startsAtLocal.value,
      capacity: form.capacity,
    })
    router.push(`/organizer/events/${ev.id}`)
  } catch (e) {
    submitError.value = e
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <h1>New event</h1>
  <p v-if="loadError" class="error">{{ loadError }}</p>

  <form v-else class="form" novalidate @submit.prevent="submit">
    <label>
      Event name
      <input v-model="form.name" required maxlength="100" placeholder="Friday Night Magic" />
      <span v-if="err('name')" class="error">{{ err('name') }}</span>
    </label>

    <label>
      Game
      <select v-model="form.gameTemplateId" required>
        <option v-for="t in templates" :key="t.id" :value="t.id">{{ t.name }}</option>
      </select>
      <span v-if="err('gameTemplateId')" class="error">{{ err('gameTemplateId') }}</span>
    </label>

    <label v-if="template">
      Format
      <select v-model="form.format">
        <option v-for="f in template.formats" :key="f" :value="f">{{ f }}</option>
      </select>
      <span v-if="err('format')" class="error">{{ err('format') }}</span>
    </label>

    <div class="row">
      <label>
        Date
        <input v-model="form.date" type="date" required />
      </label>
      <label>
        <span>Start time <span v-if="storeZone" class="muted">({{ storeZone }})</span></span>
        <input v-model="form.time" type="time" required />
      </label>
    </div>
    <p v-if="store" class="small" :class="{ notice: browserDiffers }">
      Times are in the store's time zone, {{ storeZoneLong }} ({{ store.timeZone }}).
      It's now <strong>{{ storeNow }} {{ storeZone }}</strong> at the store<span v-if="browserDiffers">,
      which differs from your computer's time zone</span>.
    </p>
    <span v-if="err('startsAtLocal')" class="error">{{ err('startsAtLocal') }}</span>
    <p v-if="template && store" class="muted small">
      Runs {{ template.defaultDurationMinutes / 60 }} h<span v-if="endsAt">, until about {{ endsAt }} {{ storeZone }}</span>
    </p>

    <label v-if="template">
      Player capacity
      <input v-model.number="form.capacity" type="number" :min="template.minPlayers" :max="maxCapacity" required />
      <span class="muted small">{{ template.name }}: {{ template.minPlayers }}–{{ maxCapacity }} players</span>
      <span v-if="err('capacity')" class="error">{{ err('capacity') }}</span>
    </label>

    <p v-if="generalError" class="error">{{ generalError }}</p>

    <div class="actions">
      <button type="submit" :disabled="submitting || !template">{{ submitting ? 'Creating…' : 'Create event' }}</button>
      <RouterLink to="/organizer">Cancel</RouterLink>
    </div>
  </form>
</template>
