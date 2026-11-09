// OTP screens (verify email, verify reset code): the six-digit code input
// and the countdown before another code can be requested.

import { useEffect, useState } from 'react';

const RESEND_WAIT_SECONDS = 60;

export function useOtpCode() {
  const [code, setDigits] = useState('');
  const [resendCountdown, setResendCountdown] = useState(RESEND_WAIT_SECONDS);

  // Count down one second at a time until a new code may be requested.
  useEffect(() => {
    if (resendCountdown <= 0) return;

    const timer = setTimeout(() => {
      setResendCountdown((previous) => Math.max(0, previous - 1));
    }, 1000);

    return () => clearTimeout(timer);
  }, [resendCountdown]);

  return {
    code,
    // Keeps digits only.
    setCode: (value: string) => setDigits(value.replace(/[^0-9]/g, '')),
    isCodeComplete: /^[0-9]{6}$/.test(code),
    resendCountdown,
    restartCountdown: () => setResendCountdown(RESEND_WAIT_SECONDS),
  };
}
