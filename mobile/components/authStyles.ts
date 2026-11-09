// Styles shared by the auth screens.
// signInStyles: Login, Register, Verify Email.
// passwordResetStyles: Forgot Password, Verify Reset Code, New Password.

import { StyleSheet } from 'react-native';

export const signInStyles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#0C0D0C',
  },
  content: {
    flexGrow: 1,
    paddingHorizontal: 28,
    paddingTop: 55,
    paddingBottom: 40,
  },
  logo: {
    color: '#C6FF35',
    fontSize: 25,
    fontWeight: '900',
    letterSpacing: 2,
  },
  heading: {
    marginTop: 65,
    marginBottom: 40,
  },
  title: {
    color: '#FFFFFF',
    fontSize: 44,
    fontWeight: '900',
    letterSpacing: -1.5,
    lineHeight: 48,
  },
  accent: {
    color: '#C6FF35',
  },
  label: {
    color: '#A5A9A5',
    fontSize: 11,
    fontWeight: '700',
    letterSpacing: 1.5,
    marginBottom: 9,
  },
  input: {
    backgroundColor: '#1C1E1C',
    color: '#FFFFFF',
    borderWidth: 1,
    borderColor: '#303330',
    borderRadius: 8,
    height: 54,
    paddingHorizontal: 16,
    fontSize: 15,
    marginBottom: 22,
  },
  button: {
    backgroundColor: '#C6FF35',
    height: 56,
    borderRadius: 8,
    alignItems: 'center',
    justifyContent: 'center',
    marginTop: 14,
  },
  disabled: {
    opacity: 0.6,
  },
  buttonText: {
    color: '#101310',
    fontWeight: '900',
    fontSize: 14,
    letterSpacing: 1,
  },
  footer: {
    color: '#A5A9A5',
    textAlign: 'center',
    marginTop: 30,
    fontSize: 13,
  },
  footerLink: {
    color: '#C6FF35',
    fontWeight: '700',
  },
});

export const passwordResetStyles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#0C0D0C',
  },
  content: {
    flexGrow: 1,
    justifyContent: 'center',
    paddingHorizontal: 28,
    paddingVertical: 48,
  },
  brand: {
    color: '#C6FF35',
    fontSize: 24,
    fontWeight: '900',
    letterSpacing: 2,
    marginBottom: 45,
  },
  heading: {
    color: '#FFFFFF',
    fontSize: 32,
    fontWeight: '900',
    letterSpacing: -1,
    marginBottom: 14,
  },
  description: {
    color: '#A0A0A0',
    fontSize: 15,
    lineHeight: 23,
    marginBottom: 35,
  },
  label: {
    color: '#FFFFFF',
    fontSize: 11,
    fontWeight: '800',
    letterSpacing: 1.5,
    marginBottom: 10,
  },
  button: {
    backgroundColor: '#C6FF35',
    paddingVertical: 18,
    borderRadius: 12,
    alignItems: 'center',
    marginTop: 8,
  },
  disabledButton: {
    opacity: 0.6,
  },
  buttonText: {
    color: '#0C0D0C',
    fontSize: 14,
    fontWeight: '900',
    letterSpacing: 1,
  },
  backButton: {
    alignItems: 'center',
    marginTop: 28,
    padding: 10,
  },
  backText: {
    color: '#C6FF35',
    fontSize: 12,
    fontWeight: '800',
    letterSpacing: 1,
  },
});
