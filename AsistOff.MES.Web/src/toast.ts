
import { useToast, POSITION } from 'vue-toastification';
import 'vue-toastification/dist/index.css';

const toast = () => useToast();

export function showSuccessToast(message: string) {
  toast().success(message, { position: POSITION.TOP_RIGHT });
}

export function showErrorToast(message: string) {
  toast().error(message, { position: POSITION.TOP_RIGHT });
}

export function showInfoToast(message: string) {
  toast().info(message, { position: POSITION.TOP_RIGHT });
}
