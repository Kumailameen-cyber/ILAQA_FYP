// Profile tab: loads the runner's profile and shows it. Edits the display
// name, bio and profile visibility here; the photo and account deletion
// live in components/ProfilePhoto and components/DeleteAccount.

import { useEffect, useState } from 'react';
import {
  View,
  Text,
  ScrollView,
  StyleSheet,
  ActivityIndicator,
  TouchableOpacity,
  Switch,
} from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { Ionicons } from '@expo/vector-icons';
import EditableProfileField from '../components/EditableProfileField';
import ProfilePhoto from '../components/ProfilePhoto';
import DeleteAccount from '../components/DeleteAccount';
import { MessagePopup, usePopup } from '../components/AppPopup';
import { authFetch, errorMessage, readErrorMessage } from '../services/api';

type RunnerProfile = {
  id: string;
  username: string;
  email: string;
  displayName: string;
  bio: string | null;
  profilePictureUrl: string | null;
  isProfilePublic: boolean;
  createdAt: string;
};

type ProfileScreenProps = {
  onDisplayNameUpdated: (newName: string) => void;
  onAccountDeleted: () => Promise<void>;
};

export default function ProfileScreen({
  onDisplayNameUpdated,
  onAccountDeleted,
}: ProfileScreenProps) {
  const [profile, setProfile] = useState<RunnerProfile | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [savingVisibility, setSavingVisibility] = useState(false);
  const { popup: notice, showPopup: showNotice, closePopup: closeNotice } =
    usePopup();

  const loadProfile = async () => {
    setLoading(true);
    setError('');

    try {
      const response = await authFetch('/api/runner/me');

      if (!response.ok) {
        throw new Error(
          response.status === 401
            ? 'Your session has expired. Please log in again.'
            : 'Unable to load your profile.'
        );
      }

      const data: RunnerProfile = await response.json();
      setProfile(data);

      // Keep the Home screen name in sync.
      onDisplayNameUpdated(data.displayName);
    } catch (err) {
      setError(errorMessage(err, 'Something went wrong.'));
    } finally {
      setLoading(false);
    }
  };

  // Load once when the tab opens.
  useEffect(() => {
    loadProfile();
  }, []);

  // Saves name, bio or visibility; the screen updates after the server agrees.
  const updateProfile = async (
    changes: Partial<
      Pick<RunnerProfile, 'displayName' | 'bio' | 'isProfilePublic'>
    >
  ) => {
    if (!profile) {
      throw new Error('Profile unavailable.');
    }

    const updatedProfile = { ...profile, ...changes };

    const response = await authFetch('/api/runner/me', {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        displayName: updatedProfile.displayName,
        bio: updatedProfile.bio,
        isProfilePublic: updatedProfile.isProfilePublic,
      }),
    });

    if (!response.ok) {
      throw new Error(
        await readErrorMessage(response, 'Unable to update profile.')
      );
    }

    setProfile(updatedProfile);

    if (changes.displayName !== undefined) {
      onDisplayNameUpdated(updatedProfile.displayName);
    }
  };

  const changeProfileVisibility = async (newValue: boolean) => {
    if (!profile || savingVisibility) return;

    setSavingVisibility(true);

    try {
      await updateProfile({ isProfilePublic: newValue });
    } catch (err) {
      showNotice(
        'error',
        'UPDATE FAILED',
        errorMessage(err, 'Unable to change profile visibility.')
      );
    } finally {
      setSavingVisibility(false);
    }
  };

  if (loading) {
    return (
      <View style={styles.center}>
        <ActivityIndicator size="large" color="#C6FF35" />
      </View>
    );
  }

  if (error || !profile) {
    return (
      <View style={styles.center}>
        <Ionicons name="alert-circle-outline" size={40} color="#FF7777" />

        <Text style={styles.errorText}>{error || 'Profile not found.'}</Text>

        <TouchableOpacity onPress={loadProfile} style={styles.retryButton}>
          <Text style={styles.retryText}>TRY AGAIN</Text>
        </TouchableOpacity>
      </View>
    );
  }

  return (
    <SafeAreaView style={styles.container} edges={['top']}>
      <ScrollView
        contentContainerStyle={styles.content}
        keyboardShouldPersistTaps="handled"
      >
        <Text style={styles.heading}>MY PROFILE</Text>

        <View style={styles.profileCard}>
          <ProfilePhoto
            pictureUrl={profile.profilePictureUrl}
            onPictureChanged={(pictureUrl) =>
              setProfile((current) =>
                current ? { ...current, profilePictureUrl: pictureUrl } : current
              )
            }
            showNotice={showNotice}
          />

          <EditableProfileField
            value={profile.displayName}
            placeholder="Display name"
            maxLength={50}
            isName
            onSave={(displayName) => updateProfile({ displayName })}
          />

          <Text style={styles.username}>@{profile.username}</Text>

          <EditableProfileField
            value={profile.bio ?? ''}
            placeholder="No bio added yet."
            maxLength={250}
            multiline
            onSave={(bio) => updateProfile({ bio })}
          />
        </View>

        <Text style={styles.sectionTitle}>ACCOUNT INFORMATION</Text>

        <View style={styles.infoCard}>
          <Text style={styles.label}>EMAIL</Text>
          <Text style={styles.value}>{profile.email}</Text>

          <View style={styles.divider} />

          <View style={styles.visibilityRow}>
            <View style={styles.visibilityInfo}>
              <Text style={styles.label}>PROFILE VISIBILITY</Text>
              <Text style={styles.value}>
                {profile.isProfilePublic ? 'Public' : 'Private'}
              </Text>
            </View>

            <Switch
              value={profile.isProfilePublic}
              onValueChange={changeProfileVisibility}
              disabled={savingVisibility}
              trackColor={{ false: '#444944', true: '#8CB52A' }}
              thumbColor={profile.isProfilePublic ? '#C6FF35' : '#BBBBBB'}
            />
          </View>

          <View style={styles.divider} />

          <Text style={styles.label}>MEMBER SINCE</Text>
          <Text style={styles.value}>
            {profile.createdAt
              ? new Date(profile.createdAt).toLocaleDateString()
              : 'Unknown'}
          </Text>
        </View>

        <DeleteAccount
          onAccountDeleted={onAccountDeleted}
          showNotice={showNotice}
        />
      </ScrollView>

      <MessagePopup popup={notice} onClose={closeNotice} />
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#0C0D0C',
  },
  content: {
    padding: 24,
    paddingBottom: 40,
  },
  center: {
    flex: 1,
    backgroundColor: '#0C0D0C',
    alignItems: 'center',
    justifyContent: 'center',
    padding: 24,
  },
  heading: {
    color: '#C6FF35',
    fontSize: 26,
    fontWeight: '900',
    letterSpacing: 2,
    marginBottom: 25,
  },
  profileCard: {
    backgroundColor: '#1C1E1C',
    borderRadius: 16,
    padding: 25,
    alignItems: 'center',
    borderWidth: 1,
    borderColor: '#303330',
  },
  username: {
    color: '#C6FF35',
    fontSize: 14,
    marginTop: 8,
  },
  sectionTitle: {
    color: '#999E99',
    fontSize: 12,
    fontWeight: '800',
    letterSpacing: 1.5,
    marginTop: 30,
    marginBottom: 12,
  },
  infoCard: {
    backgroundColor: '#1C1E1C',
    borderRadius: 12,
    padding: 20,
  },
  label: {
    color: '#777D77',
    fontSize: 11,
    fontWeight: '800',
    marginBottom: 7,
  },
  value: {
    color: '#FFFFFF',
    fontSize: 15,
    marginBottom: 12,
  },
  divider: {
    height: 1,
    backgroundColor: '#303330',
    marginVertical: 12,
  },
  visibilityRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
  visibilityInfo: {
    flex: 1,
  },
  retryButton: {
    marginTop: 20,
    padding: 15,
  },
  retryText: {
    color: '#C6FF35',
    fontWeight: '800',
  },
  errorText: {
    color: '#FF7777',
    textAlign: 'center',
    marginTop: 12,
  },
});
