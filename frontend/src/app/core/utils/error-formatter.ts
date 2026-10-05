import { HttpErrorResponse } from '@angular/common/http';
import { ApiError } from '../models/auth.models';

export function extractErrorMessage(error: unknown, fallback: string): string {
  if (!error) {
    return fallback;
  }

  if (error instanceof HttpErrorResponse) {
    const errorBody = error.error as ApiError | string | null;

    if (errorBody && typeof errorBody === 'object') {
      // 1. Check for specific ProblemDetails 'detail'
      if (errorBody.detail && typeof errorBody.detail === 'string') {
        return errorBody.detail;
      }

      // 2. Check for validation errors dictionary
      if (errorBody.errors && typeof errorBody.errors === 'object') {
        const fieldErrors = Object.values(errorBody.errors).flat();
        if (fieldErrors.length > 0 && typeof fieldErrors[0] === 'string') {
          return fieldErrors.join(' ');
        }
      }

      // 3. Check for 'title'
      if (errorBody.title && typeof errorBody.title === 'string' && errorBody.title !== 'One or more validation errors occurred.') {
        return errorBody.title;
      }
    }

    if (typeof errorBody === 'string' && errorBody.trim().length > 0) {
      return errorBody;
    }

    if (error.status === 401) {
      return 'Invalid email or password. Please verify your credentials.';
    }

    if (error.status === 400) {
      return 'Bad request. Please review the form fields and try again.';
    }

    if (error.status === 0) {
      return 'Unable to reach the server. Please check your backend connection.';
    }
  }

  return fallback;
}
