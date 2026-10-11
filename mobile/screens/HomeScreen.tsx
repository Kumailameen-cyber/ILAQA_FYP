
import React from 'react';
import {
  View,
  Text,
  TouchableOpacity,
  StyleSheet,
} from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { StatusBar } from 'expo-status-bar';

type HomeScreenProps = {
  displayName: string;
  onLogout: () => void;
};

export default function HomeScreen({
  displayName,
  onLogout,
}: HomeScreenProps) {
  return (
    <SafeAreaView style={styles.container}>
      <StatusBar style="light" />

      <View style={styles.header}>
        <Text style={styles.logo}>ILAQA®</Text>
        <Text style={styles.headerLabel}>RUNNER DASHBOARD</Text>
      </View>

      <View style={styles.content}>
        <Text style={styles.greeting}>WELCOME BACK,</Text>

        <Text style={styles.name}>
          {displayName.toUpperCase()}
        </Text>

        <Text style={styles.subtitle}>
          Your city. Your run. Your territory.
        </Text>

        <View style={styles.placeholder}>
          <Text style={styles.placeholderTitle}>
            YOUR MAP IS COMING.
          </Text>

          <Text style={styles.placeholderText}>
            Soon, you'll be able to track your runs,
            explore Karachi, and claim territory.
          </Text>
        </View>
      </View>

      <TouchableOpacity
        style={styles.logoutButton}
        onPress={onLogout}
      >
        <Text style={styles.logoutText}>
          LOG OUT
        </Text>
      </TouchableOpacity>
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#0C0D0C',
    paddingHorizontal: 28,
  },
  header: {
    paddingTop: 35,
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
  logo: {
    color: '#C6FF35',
    fontSize: 25,
    fontWeight: '900',
    letterSpacing: 2,
  },
  headerLabel: {
    color: '#777D77',
    fontSize: 10,
    fontWeight: '700',
    letterSpacing: 1,
  },
  content: {
    flex: 1,
    justifyContent: 'center',
  },
  greeting: {
    color: '#999E99',
    fontSize: 14,
    fontWeight: '700',
    letterSpacing: 2,
  },
  name: {
    color: '#C6FF35',
    fontSize: 42,
    fontWeight: '900',
    marginTop: 10,
  },
  subtitle: {
    color: '#FFFFFF',
    fontSize: 15,
    marginTop: 12,
  },
  placeholder: {
    backgroundColor: '#1C1E1C',
    borderWidth: 1,
    borderColor: '#303330',
    borderRadius: 12,
    padding: 25,
    marginTop: 55,
  },
  placeholderTitle: {
    color: '#FFFFFF',
    fontSize: 20,
    fontWeight: '900',
    marginBottom: 12,
  },
  placeholderText: {
    color: '#999E99',
    fontSize: 14,
    lineHeight: 23,
  },
  logoutButton: {
    borderWidth: 1,
    borderColor: '#454A45',
    borderRadius: 8,
    height: 54,
    alignItems: 'center',
    justifyContent: 'center',
    marginBottom: 25,
  },
  logoutText: {
    color: '#FFFFFF',
    fontWeight: '800',
    fontSize: 13,
    letterSpacing: 1.5,
  },
});
