import axios from 'axios';

const http = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL,
});

http.interceptors.request.use(
  (config) => {
    const token = localStorage.getItem('token');
    if (token) {
      config.headers.Authorization = `Bearer ${token}`;
    }
    
    if (config.data && typeof config.data === 'object') {
      config.headers['Content-Type'] = 'application/json';
    }
    
    return config;
  },
  (error) => {
    return Promise.reject(error);
  }
);

http.interceptors.response.use(
  (response) => {
    return response;
  },
  (error) => {
    if (error.response?.status === 401) {
      const errorMessage = error.response?.data?.detail || error.response?.data?.message || '';
      
      // Check if this is a permission issue vs invalid token
      if (errorMessage.includes('permission') || errorMessage.includes('authorize')) {
        return Promise.reject(new Error('You do not have permission to access this resource'));
      }
      
      // Clear localStorage
      localStorage.removeItem('token');
      localStorage.removeItem('user');
      
      // Only redirect if not already on login/register page
      const currentPath = window.location.pathname;
      if (!currentPath.includes('/login') && 
          !currentPath.includes('/register') && 
          currentPath !== '/') {
        window.location.href = '/';
      }
    }
    
    return Promise.reject(error);
  }
);

export default http;
