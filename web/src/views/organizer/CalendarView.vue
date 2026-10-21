<script setup lang="ts">
import type { CalendarOptions, EventInput, EventSourceFuncArg } from '@fullcalendar/core'
import dayGridPlugin from '@fullcalendar/daygrid'
import interactionPlugin from '@fullcalendar/interaction'
import listPlugin from '@fullcalendar/list'
import FullCalendar from '@fullcalendar/vue3'
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { api, type EventDto } from '../../api'
import { errorMessage } from '../../lib/errors'
import { toStoreWallTime } from '../../lib/time'

const router = useRouter()
const error = ref<string>()
const storeName = ref<string>()

const DAY_MS = 24 * 60 * 60 * 1000
const PALETTE = ['#2563eb', '#9333ea', '#0d9488', '#c2410c', '#be123c', '#4d7c0f']

/** A stable color per game id. Works for any template, with no per-game code. */
function gameColor(gameId: string): string {
  let hash = 0
  for (const ch of gameId) hash = (hash * 31 + ch.charCodeAt(0)) >>> 0
  return PALETTE[hash % PALETTE.length]
}

function toCalendarEvent(e: EventDto, timeZone: string): EventInput {
  return {
    id: e.id,
    title: `${e.name} · ${e.gameName} · ${e.registeredCount}/${e.capacity}${e.isFull ? ' FULL' : ''}`,
    start: toStoreWallTime(e.startsAtUtc, timeZone),
    end: toStoreWallTime(e.endsAtUtc, timeZone),
    color: gameColor(e.gameTemplateId),
    classNames: e.isFull ? ['ev-full'] : [],
  }
}

async function loadEvents(info: EventSourceFuncArg): Promise<EventInput[]> {
  try {
    const store = await api.store()
    storeName.value = store.name
    // The calendar runs in UTC mode with store wall times, so pad the range by a day each
    // side to cover the store's UTC offset. Events outside the view are simply not drawn.
    const from = new Date(info.start.getTime() - DAY_MS).toISOString()
    const to = new Date(info.end.getTime() + DAY_MS).toISOString()
    const events = await api.events(from, to)
    error.value = undefined
    return events.map((e) => toCalendarEvent(e, store.timeZone))
  } catch (e) {
    error.value = errorMessage(e)
    return []
  }
}

const options: CalendarOptions = {
  plugins: [dayGridPlugin, listPlugin, interactionPlugin],
  initialView: 'dayGridMonth',
  timeZone: 'UTC', // see toStoreWallTime: events are already store-local wall times
  headerToolbar: { left: 'prev,next today', center: 'title', right: 'dayGridMonth,listMonth' },
  buttonText: { month: 'Month', list: 'Agenda' },
  height: 'auto',
  dayMaxEvents: 3,
  events: loadEvents,
  eventClick: (arg) => {
    arg.jsEvent.preventDefault()
    router.push(`/organizer/events/${arg.event.id}`)
  },
  dateClick: (arg) => router.push({ path: '/organizer/events/new', query: { date: arg.dateStr } }),
}
</script>

<template>
  <div class="page-head">
    <h1>Events<span v-if="storeName" class="muted"> · {{ storeName }}</span></h1>
    <RouterLink class="button" to="/organizer/events/new">New event</RouterLink>
  </div>
  <p class="muted small">Times are in the store's time zone. Click a day to schedule an event on it.</p>
  <p v-if="error" class="error">{{ error }}</p>
  <FullCalendar :options="options" />
</template>
