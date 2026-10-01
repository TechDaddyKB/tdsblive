import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { Login } from './Login';
import { api } from './api';

afterEach(() => { cleanup(); vi.restoreAllMocks(); });

it('signs in and clears the credential without browser storage', async () => {
  vi.spyOn(api, 'login').mockResolvedValue();
  const authenticated = vi.fn();
  render(<Login onAuthenticated={authenticated} />);
  fireEvent.change(screen.getByLabelText(/Admin credential/), { target: { value: 'synthetic-test-credential' } });
  fireEvent.click(screen.getByRole('button', { name: 'Sign in' }));
  await waitFor(() => expect(authenticated).toHaveBeenCalledOnce());
  expect(screen.getByLabelText(/Admin credential/)).toHaveValue('');
});

it('shows a generic failure without echoing credentials', async () => {
  vi.spyOn(api, 'login').mockRejectedValue(new Error('synthetic-test-credential'));
  render(<Login />);
  fireEvent.change(screen.getByLabelText(/Admin credential/), { target: { value: 'synthetic-test-credential' } });
  fireEvent.click(screen.getByRole('button', { name: 'Sign in' }));
  expect(await screen.findByRole('alert')).toHaveTextContent('Sign in failed');
  expect(screen.getByRole('alert')).not.toHaveTextContent('synthetic-test-credential');
});
