<script setup lang="ts">
import QRCode from 'qrcode'
import { ref, watchEffect } from 'vue'

const props = defineProps<{ value: string; size?: number }>()
const dataUrl = ref<string>()

watchEffect(async () => {
  dataUrl.value = await QRCode.toDataURL(props.value, { width: props.size ?? 240, margin: 1 })
})
</script>

<template>
  <img v-if="dataUrl" :src="dataUrl" :width="size ?? 240" :height="size ?? 240" :alt="`QR code for ${value}`" />
</template>
