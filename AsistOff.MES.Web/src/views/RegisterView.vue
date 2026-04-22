<template>
  <div class="login-bg">
    <div class="login-container">
      <div class="login-header">
        <i class="pi pi-cog login-cog"></i>
        <span class="login-title">{{ $t('register.title') }}</span>
      </div>
      <form class="login-form" @submit.prevent="onRegister">
        <div class="login-field">
          <CustomInput
            id="reg-username"
            :label="$t('register.username')"
            v-model="regUsername"
            required
          />
        </div>
        <div class="login-field">
          <CustomInput
            id="reg-password"
            :label="$t('register.password')"
            type="password"
            v-model="regPassword"
            required
          />
        </div>
        <div class="login-field">
          <CustomInput
            id="reg-confirm"
            :label="$t('register.confirmPassword')"
            type="password"
            v-model="regConfirm"
            required
          />
        </div>
        <button type="submit" class="p-button p-component login-btn">{{ $t('register.submit') }}</button>
        <button type="button" class="register-link" @click="goLogin">{{ $t('register.login') }}</button>
      </form>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import CustomInput from '../components/CustomInput.vue';
import { showSuccessToast, showErrorToast } from '../toast';

const regUsername = ref('')
const regPassword = ref('')
const regConfirm = ref('')
const router = useRouter()

function onRegister() {
  if (regPassword.value !== regConfirm.value) {
    showErrorToast('Passwords do not match!')
    return
  }
  showSuccessToast(`Registered as ${regUsername.value}`)
  router.push('/')
}

function goLogin() {
  router.push('/')
}
</script>

<style scoped>
.login-cog {
  font-size: 2.5rem;
  color: #f9d423;
  animation: spin 2.5s linear infinite;
}
@keyframes spin {
  0% { transform: rotate(0deg); }
  100% { transform: rotate(360deg); }
}
</style>
