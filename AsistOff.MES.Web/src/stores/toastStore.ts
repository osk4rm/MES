import { defineStore } from 'pinia';

export type ToastVariant = 'success' | 'error' | 'info' | 'warning';

export interface Toast {
  id: number;
  variant: ToastVariant;
  message: string;
  timeout: number;
}

let nextId = 1;

export const useToastStore = defineStore('toast', {
  state: () => ({
    toasts: [] as Toast[]
  }),
  actions: {
    push(variant: ToastVariant, message: string, timeout = 3500) {
      const id = nextId++;
      this.toasts.push({ id, variant, message, timeout });
      if (timeout > 0) {
        setTimeout(() => this.dismiss(id), timeout);
      }
      return id;
    },
    dismiss(id: number) {
      this.toasts = this.toasts.filter(t => t.id !== id);
    },
    success(message: string, timeout?: number) { return this.push('success', message, timeout); },
    error(message: string, timeout?: number) { return this.push('error', message, timeout ?? 5000); },
    info(message: string, timeout?: number) { return this.push('info', message, timeout); },
    warning(message: string, timeout?: number) { return this.push('warning', message, timeout); }
  }
});
