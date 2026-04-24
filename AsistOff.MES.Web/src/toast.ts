import { useToastStore } from './stores/toastStore';

export function showSuccessToast(message: string) {
  useToastStore().success(message);
}
export function showErrorToast(message: string) {
  useToastStore().error(message);
}
export function showInfoToast(message: string) {
  useToastStore().info(message);
}
export function showWarningToast(message: string) {
  useToastStore().warning(message);
}
