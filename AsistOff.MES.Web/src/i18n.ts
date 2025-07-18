import { createI18n } from 'vue-i18n';

const messages = {
  en: {
    login: {
      title: 'Login',
      email: 'Email',
      password: 'Password',
      submit: 'Sign In',
      register: 'Register',
    },
    register: {
      title: 'Register',
      username: 'Username',
      password: 'Password',
      confirmPassword: 'Confirm Password',
      submit: 'Sign Up',
      login: 'Back to Login',
    },
    // Add more translations as needed
  },
  pl: {
    login: {
      title: 'Logowanie',
      email: 'Email',
      password: 'Hasło',
      submit: 'Zaloguj się',
      register: 'Rejestracja',
    },
    register: {
      title: 'Rejestracja',
      username: 'Nazwa użytkownika',
      password: 'Hasło',
      confirmPassword: 'Potwierdź hasło',
      submit: 'Zarejestruj się',
      login: 'Powrót do logowania',
    },
    // Add more translations as needed
  },
};

const i18n = createI18n({
  legacy: false,
  locale: 'en',
  fallbackLocale: 'en',
  messages,
});

export default i18n;
