import { createRouter, createWebHistory } from 'vue-router'

export default createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/', redirect: '/organizer' },
    { path: '/organizer', component: () => import('../views/organizer/CalendarView.vue') },
    { path: '/organizer/events/new', component: () => import('../views/organizer/CreateEventView.vue') },
    { path: '/organizer/events/:id', component: () => import('../views/organizer/EventDetailView.vue'), props: true },
    { path: '/e/:id', component: () => import('../views/player/PublicEventView.vue'), props: true },
    { path: '/:pathMatch(.*)*', component: () => import('../views/NotFoundView.vue') },
  ],
})
