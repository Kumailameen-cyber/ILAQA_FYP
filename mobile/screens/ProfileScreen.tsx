
import React, { useEffect, useState } from 'react';
import {
  View,
  Text,
  ScrollView,
  StyleSheet,
  ActivityIndicator,
  TouchableOpacity,
  Switch,
  Alert,
  Image,
  TextInput ,
  Modal
} from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { Ionicons } from '@expo/vector-icons';
import * as SecureStore from 'expo-secure-store';
import * as ImagePicker from 'expo-image-picker';
import EditableProfileField from '../components/EditableProfileField';
import { File } from 'expo-file-system';
import PasswordInput from '../components/PasswordInput';
import AppPopup from '../components/AppPopup';
const API_URL = 'http://192.168.100.232:5246';

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
  const [uploadingPicture, setUploadingPicture] = useState(false);
  const [viewingPhoto, setViewingPhoto] = useState(false);
  const [selectedPhoto, setSelectedPhoto] =
  useState<ImagePicker.ImagePickerAsset | null>(null);
  const [photoMenuVisible, setPhotoMenuVisible] = useState(false);
  const [deleteModalVisible, setDeleteModalVisible] = useState(false);
const [deletePassword, setDeletePassword] = useState('');
const [deletingAccount, setDeletingAccount] = useState(false);
const [confirmDeleteVisible, setConfirmDeleteVisible] = useState(false);
const [confirmRemovePhotoVisible, setConfirmRemovePhotoVisible] = useState(false);



type NoticeType = 'success' | 'error' | 'warning';

const [notice, setNotice] = useState<{
  type: NoticeType;
  title: string;
  message: string;
} | null>(null);

const showNotice = (
  type: NoticeType,
  title: string,
  message: string
) => {
  setNotice({ type, title, message });
};

  // Load profile from backend
  const loadProfile = async () => {
    setLoading(true);
    setError('');

    try {
      const token = await SecureStore.getItemAsync('accessToken');

      if (!token) {
        throw new Error('Please log in again.');
      }

      const response = await fetch(`${API_URL}/api/runner/me`, {
        headers: {
          Authorization: `Bearer ${token}`,
        },
      });

      if (!response.ok) {
        throw new Error(
          response.status === 401
            ? 'Your session has expired. Please log in again.'
            : 'Unable to load your profile.'
        );
      }

      const data: RunnerProfile = await response.json();
      setProfile(data);

      // Keep Home screen name synchronized
      onDisplayNameUpdated(data.displayName);
    } catch (err) {
      setError(
        err instanceof Error
          ? err.message
          : 'Something went wrong.'
      );
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadProfile();
  }, []);

  // One reusable backend update function
  const updateProfile = async (
    changes: Partial<
      Pick<
        RunnerProfile,
        'displayName' | 'bio' | 'isProfilePublic'
      >
    >
  ) => {
    if (!profile) {
      throw new Error('Profile unavailable.');
    }

    const updatedProfile = {
      ...profile,
      ...changes,
    };

    const token = await SecureStore.getItemAsync('accessToken');

    if (!token) {
      throw new Error('Please log in again.');
    }

    const response = await fetch(`${API_URL}/api/runner/me`, {
      method: 'PUT',
      headers: {
        Authorization: `Bearer ${token}`,
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({
        displayName: updatedProfile.displayName,
        bio: updatedProfile.bio,
        isProfilePublic: updatedProfile.isProfilePublic,
      }),
    });

    if (!response.ok) {
      const data = await response.json().catch(() => null);

      throw new Error(
        data?.message || 'Unable to update profile.'
      );
    }

    // Update the UI after the backend confirms success
    setProfile(updatedProfile);

    if (changes.displayName !== undefined) {
      onDisplayNameUpdated(updatedProfile.displayName);
    }
  };
const changeProfilePicture = async () => {
  if (!profile || uploadingPicture) return;

  try {
    const permission = await ImagePicker.requestMediaLibraryPermissionsAsync();
    if (!permission.granted) {
      showNotice(
  'warning',
  'PERMISSION REQUIRED',
  'Please allow access to your photos.'
);
      return;
    }

    const result = await ImagePicker.launchImageLibraryAsync({
      mediaTypes: ['images'],
      allowsEditing: true,
      aspect: [1, 1],
      quality: 0.7,
    });

    if (result.canceled || !result.assets?.length) return;

    const image = result.assets[0];
    if (image.fileSize !== undefined && image.fileSize > 2 * 1024 * 1024) {
      showNotice(
  'warning',
  'IMAGE TOO LARGE',
  'Please choose an image smaller than 2 MB.'
);
      return;
    }

    // Cropping only opens the preview. It does not upload.
    setSelectedPhoto(image);
  } catch (err) {
    showNotice(
  'error',
  'PHOTO SELECTION FAILED',
  err instanceof Error ? err.message : 'Could not select photo.'
);
  }
};

const uploadProfilePicture = async () => {
  if (!selectedPhoto || uploadingPicture) return;

  try {
    setUploadingPicture(true);
    const token = await SecureStore.getItemAsync('accessToken');
    if (!token) throw new Error('Please log in again.');

    // Preserve the Expo File upload method already tested on Android.
    const imageFile = new File(selectedPhoto.uri);
    const formData = new FormData();
    formData.append(
      'file',
      imageFile,
      selectedPhoto.fileName || 'profile.jpg'
    );

    const response = await fetch(`${API_URL}/api/runner/me/profile-picture`, {
      method: 'POST',
      headers: { Authorization: `Bearer ${token}` },
      body: formData,
    });

    if (!response.ok) {
      const data = await response.json().catch(() => null);
      throw new Error(data?.message || 'Unable to upload profile picture.');
    }

    const data = await response.json();
    setProfile((current) => current
      ? { ...current, profilePictureUrl: data.profilePictureUrl }
      : current
    );
    setSelectedPhoto(null);
    showNotice('success', 'PHOTO UPDATED', 'Profile picture updated!');
  } catch (err) {
    showNotice(
  'error',
  'UPLOAD FAILED',
  err instanceof Error ? err.message : 'Something went wrong.'
);
  } finally {
    setUploadingPicture(false);
  }
};

const removeProfilePicture = async () => {
  if (!profile?.profilePictureUrl || uploadingPicture) return;

  try {
    setUploadingPicture(true);

    const token = await SecureStore.getItemAsync('accessToken');

    if (!token) {
      throw new Error('Please log in again.');
    }

    const response = await fetch(
      `${API_URL}/api/runner/me/profile-picture`,
      {
        method: 'DELETE',
        headers: {
          Authorization: `Bearer ${token}`,
        },
      }
    );

    if (!response.ok) {
      const data = await response.json().catch(() => null);
      throw new Error(
        data?.message || 'Unable to remove profile picture.'
      );
    }

    setProfile((current) =>
      current
        ? { ...current, profilePictureUrl: null }
        : current
    );

    showNotice('success', 'PHOTO REMOVED', 'Profile picture removed.');
  } catch (err) {
    showNotice(
  'error',
  'REMOVE FAILED',
  err instanceof Error ? err.message : 'Something went wrong.'
);
  } finally {
    setUploadingPicture(false);
  }
};


const deleteMyAccount = async () => {
  if (deletingAccount || !deletePassword.trim()) return;

  setDeletingAccount(true);

  try {
    const token = await SecureStore.getItemAsync('accessToken');

    if (!token) {
      throw new Error('Please log in again.');
    }

    const response = await fetch(`${API_URL}/api/runner/me`, {
      method: 'DELETE',
      headers: {
        Authorization: `Bearer ${token}`,
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({
        password: deletePassword,
      }),
    });

    if (!response.ok) {
      const data = await response.json().catch(() => null);
      throw new Error(
        data?.message || 'Unable to delete account.'
      );
    }

    // Backend confirmed that the account was deleted.
    setDeletePassword('');
    setDeleteModalVisible(false);

    // Clear local login tokens and return to Login.
    await onAccountDeleted();
  } catch (err) {
    showNotice(
  'error',
  'ACCOUNT DELETION FAILED',
  err instanceof Error ? err.message : 'Something went wrong.'
);
  } finally {
    setDeletingAccount(false);
  }
};


const openProfilePhotoMenu = () => {
  setPhotoMenuVisible(true);
};



  // Display name uses the shared update function
  const saveDisplayName = async (newName: string) => {
    await updateProfile({
      displayName: newName,
    });
  };

  // Bio uses the same shared update function
  const saveBio = async (newBio: string) => {
    await updateProfile({
      bio: newBio,
    });
  };
const changeProfileVisibility = async (newValue: boolean) => {
  if (!profile || savingVisibility) return;

  setSavingVisibility(true);

  try {
    await updateProfile({
      isProfilePublic: newValue,
    });
  } catch (err) {
   showNotice(
  'error',
  'UPDATE FAILED',
  err instanceof Error
    ? err.message
    : 'Unable to change profile visibility.'
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
        <Ionicons
          name="alert-circle-outline"
          size={40}
          color="#FF7777"
        />

        <Text style={styles.errorText}>
          {error || 'Profile not found.'}
        </Text>

        <TouchableOpacity
          onPress={loadProfile}
          style={styles.retryButton}
        >
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
          {/* Profile picture placeholder */}
          
<TouchableOpacity
  style={styles.avatar}
  onPress={openProfilePhotoMenu}
  disabled={uploadingPicture}
  accessibilityLabel="Change profile picture"
>
  {uploadingPicture ? (
    <ActivityIndicator size="large" color="#C6FF35" />
  ) : profile.profilePictureUrl ? (
    <Image
      source={{ uri: profile.profilePictureUrl }}
      style={styles.profileImage}
    />
  ) : (
    <Ionicons
      name="person"
      size={40}
      color="#C6FF35"
    />
  )}

  {!uploadingPicture && (
    <View style={styles.cameraBadge}>
      <Ionicons name="camera" size={15} color="#0C0D0C" />
    </View>
  )}
</TouchableOpacity>


          {/* Reusable display-name editor */}
          <EditableProfileField
            value={profile.displayName}
            placeholder="Display name"
            maxLength={50}
            isName
            onSave={saveDisplayName}
          />

          <Text style={styles.username}>
            @{profile.username}
          </Text>

          {/* Same component, configured for bio */}
          <EditableProfileField
            value={profile.bio ?? ''}
            placeholder="No bio added yet."
            maxLength={250}
            multiline
            onSave={saveBio}
          />
        </View>

        <Text style={styles.sectionTitle}>
          ACCOUNT INFORMATION
        </Text>

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
    trackColor={{
      false: '#444944',
      true: '#8CB52A',
    }}
    thumbColor={
      profile.isProfilePublic ? '#C6FF35' : '#BBBBBB'
    }
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
        
<TouchableOpacity
  style={styles.deleteAccountButton}
  onPress={() => setDeleteModalVisible(true)}
>
  <Ionicons
    name="trash-outline"
    size={20}
    color="#FF7777"
  />
  <Text style={styles.deleteAccountButtonText}>
    DELETE ACCOUNT
  </Text>
</TouchableOpacity>

      </ScrollView>

      
<Modal
  visible={viewingPhoto}
  transparent
  animationType="fade"
  onRequestClose={() => setViewingPhoto(false)}
>
  <View style={styles.photoViewer}>
    <TouchableOpacity
      style={styles.closePhotoButton}
      onPress={() => setViewingPhoto(false)}
    >
      <Ionicons name="close" size={28} color="#FFFFFF" />
    </TouchableOpacity>

    {profile?.profilePictureUrl && (
      <Image
        source={{ uri: profile.profilePictureUrl }}
        style={styles.fullProfileImage}
        resizeMode="contain"
      />
    )}
  </View>
</Modal>

      {/* Confirm the cropped photo before uploading */}
      <Modal
        visible={selectedPhoto !== null}
        transparent
        animationType="fade"
        onRequestClose={() => {
          if (!uploadingPicture) setSelectedPhoto(null);
        }}
      >
        <View style={styles.previewOverlay}>
          <View style={styles.previewCard}>
            <Text style={styles.previewTitle}>PREVIEW PHOTO</Text>
            <Text style={styles.previewSubtitle}>
              Ready to update your profile picture?
            </Text>

            {selectedPhoto && (
              <Image
                source={{ uri: selectedPhoto.uri }}
                style={styles.previewImage}
                resizeMode="cover"
              />
            )}

            <TouchableOpacity
              style={styles.uploadPhotoButton}
              onPress={uploadProfilePicture}
              disabled={uploadingPicture}
            >
              {uploadingPicture ? (
                <ActivityIndicator color="#0C0D0C" />
              ) : (
                <Text style={styles.uploadPhotoText}>UPLOAD PHOTO</Text>
              )}
            </TouchableOpacity>

            <TouchableOpacity
              style={styles.cancelPhotoButton}
              onPress={() => setSelectedPhoto(null)}
              disabled={uploadingPicture}
            >
              <Text style={styles.cancelPhotoText}>CANCEL</Text>
            </TouchableOpacity>
          </View>
        </View>
      </Modal>

{/* Profile picture options */}
<Modal
  
  visible={photoMenuVisible && !confirmRemovePhotoVisible}
  transparent
  animationType="fade"
  onRequestClose={() => setPhotoMenuVisible(false)}
>
  <View style={styles.menuOverlay}>
    <View style={styles.menuCard}>

      <View style={styles.menuHeader}>
        <Text style={styles.menuTitle}>
          PROFILE PICTURE
        </Text>

        <TouchableOpacity
          onPress={() => setPhotoMenuVisible(false)}
          accessibilityLabel="Close menu"
        >
          <Ionicons name="close" size={26} color="#FFFFFF" />
        </TouchableOpacity>
      </View>

      {profile?.profilePictureUrl && (
        <TouchableOpacity
          style={styles.menuOption}
          onPress={() => {
            setPhotoMenuVisible(false);
            setViewingPhoto(true);
          }}
        >
          <Ionicons name="eye-outline" size={22} color="#C6FF35" />
          <Text style={styles.menuOptionText}>View Photo</Text>
        </TouchableOpacity>
      )}

      <TouchableOpacity
        style={styles.menuOption}
        onPress={() => {
          setPhotoMenuVisible(false);
          changeProfilePicture();
        }}
      >
        <Ionicons name="image-outline" size={22} color="#C6FF35" />
        <Text style={styles.menuOptionText}>
          {profile?.profilePictureUrl ? 'Change Photo' : 'Upload Photo'}
        </Text>
      </TouchableOpacity>

      {profile?.profilePictureUrl && (
        <TouchableOpacity
          style={styles.menuOption}
        onPress={() => {
  setPhotoMenuVisible(false);
  setConfirmRemovePhotoVisible(true);
}}
        >
          <Ionicons name="trash-outline" size={22} color="#FF7777" />
          <Text style={[styles.menuOptionText, { color: '#FF7777' }]}>
            Remove Photo
          </Text>
        </TouchableOpacity>
      )}

    </View>
  </View>
</Modal>

{/* Delete Account confirmation */}
<Modal
    visible={deleteModalVisible && !confirmDeleteVisible}
  transparent
  animationType="fade"
  onRequestClose={() => {
    if (!deletingAccount) {
      setDeleteModalVisible(false);
      setDeletePassword('');
    }
  }}
>
  <View style={styles.deleteOverlay}>
    <View style={styles.deleteCard}>
      <View style={styles.deleteHeader}>
        <Text style={styles.deleteTitle}>DELETE ACCOUNT</Text>

        <TouchableOpacity
          disabled={deletingAccount}
          onPress={() => {
            setDeleteModalVisible(false);
            setDeletePassword('');
          }}
        >
          <Ionicons name="close" size={26} color="#FFFFFF" />
        </TouchableOpacity>
      </View>

      <Text style={styles.deleteWarning}>
        This action is permanent. Your account and associated
        data will be deleted and cannot be recovered.
      </Text>

      <Text style={styles.deleteLabel}>
        ENTER YOUR PASSWORD TO CONFIRM
      </Text>

    <PasswordInput
  value={deletePassword}
  onChangeText={setDeletePassword}
  placeholder="Current password"
  autoComplete="current-password"
  editable={!deletingAccount}
/>

      <TouchableOpacity
        style={[
          styles.confirmDeleteButton,
          (!deletePassword.trim() || deletingAccount) && {
            opacity: 0.5,
          },
        ]}
       onPress={() => setConfirmDeleteVisible(true)}
        disabled={!deletePassword.trim() || deletingAccount}
      >
        {deletingAccount ? (
          <ActivityIndicator color="#FFFFFF" />
        ) : (
          <Text style={styles.confirmDeleteText}>
            DELETE PERMANENTLY
          </Text>
        )}
      </TouchableOpacity>
    </View>
  </View>
</Modal>

<AppPopup
  visible={confirmDeleteVisible}
  type="confirm"
  title="DELETE ACCOUNT?"
  message="This action is permanent. Your account and associated data cannot be recovered."
  confirmText="DELETE"
  cancelText="CANCEL"
  loading={deletingAccount}
  onClose={() => setConfirmDeleteVisible(false)}
  onConfirm={() => {
    setConfirmDeleteVisible(false);
    deleteMyAccount();
  }}
/>

<AppPopup
  visible={notice !== null}
  type={notice?.type ?? 'error'}
  title={notice?.title ?? ''}
  message={notice?.message ?? ''}
  onClose={() => setNotice(null)}
/>
<AppPopup
  visible={confirmRemovePhotoVisible}
  type="confirm"
  title="REMOVE PROFILE PHOTO?"
  message="Are you sure you want to remove your profile picture?"
  confirmText="REMOVE"
  cancelText="CANCEL"
  loading={uploadingPicture}
  onClose={() => setConfirmRemovePhotoVisible(false)}
  onConfirm={() => {
    setConfirmRemovePhotoVisible(false);
    removeProfilePicture();
  }}
/>


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
  avatar: {
    width: 90,
    height: 90,
    borderRadius: 45,
    backgroundColor: '#303830',
    justifyContent: 'center',
    alignItems: 'center',
    marginBottom: 15,
    position: 'relative',
  },
  profileImage: {
  width: 90,
  height: 90,
  borderRadius: 45,
},

cameraBadge: {
  position: 'absolute',
  bottom: 0,
  right: 0,
  width: 28,
  height: 28,
  borderRadius: 14,
  backgroundColor: '#C6FF35',
  alignItems: 'center',
  justifyContent: 'center',
  borderWidth: 2,
  borderColor: '#1C1E1C',
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
  },visibilityRow: {
  flexDirection: 'row',
  justifyContent: 'space-between',
  alignItems: 'center',
},

visibilityInfo: {
  flex: 1,
},
photoViewer: {
  flex: 1,
  backgroundColor: '#0C0D0C',
  justifyContent: 'center',
  alignItems: 'center',
},

closePhotoButton: {
  position: 'absolute',
  top: 55,
  right: 25,
  zIndex: 10,
},

fullProfileImage: {
  width: '100%',
  height: '75%',
},
  previewOverlay: {
    flex: 1,
    backgroundColor: 'rgba(0,0,0,0.85)',
    justifyContent: 'center',
    alignItems: 'center',
    padding: 24,
  },
  previewCard: {
    width: '100%',
    backgroundColor: '#1A1C1A',
    borderRadius: 20,
    padding: 24,
    alignItems: 'center',
    borderWidth: 1,
    borderColor: '#333833',
  },
  previewTitle: {
    color: '#C6FF35',
    fontSize: 20,
    fontWeight: '800',
    letterSpacing: 1,
    marginBottom: 8,
  },
  previewSubtitle: {
    color: '#AAAAAA',
    fontSize: 13,
    textAlign: 'center',
    marginBottom: 24,
  },
  previewImage: {
    width: 220,
    height: 220,
    borderRadius: 110,
    borderWidth: 3,
    borderColor: '#C6FF35',
    marginBottom: 28,
  },
  uploadPhotoButton: {
    backgroundColor: '#C6FF35',
    width: '100%',
    paddingVertical: 15,
    borderRadius: 12,
    alignItems: 'center',
    marginBottom: 12,
  },
  uploadPhotoText: {
    color: '#0C0D0C',
    fontSize: 15,
    fontWeight: '800',
  },
  cancelPhotoButton: {
    width: '100%',
    paddingVertical: 14,
    alignItems: 'center',
  },
  cancelPhotoText: {
    color: '#FFFFFF',
    fontSize: 14,
    fontWeight: '600',
  },
  
menuOverlay: {
  flex: 1,
  backgroundColor: 'rgba(0,0,0,0.8)',
  justifyContent: 'center',
  padding: 24,
},

menuCard: {
  backgroundColor: '#1C1E1C',
  borderRadius: 20,
  padding: 24,
  borderWidth: 1,
  borderColor: '#303330',
},

menuHeader: {
  flexDirection: 'row',
  alignItems: 'center',
  justifyContent: 'space-between',
  marginBottom: 20,
},

menuTitle: {
  color: '#C6FF35',
  fontSize: 19,
  fontWeight: '800',
  letterSpacing: 1,
},

menuOption: {
  flexDirection: 'row',
  alignItems: 'center',
  gap: 14,
  paddingVertical: 17,
  borderBottomWidth: 1,
  borderBottomColor: '#303330',
},

menuOptionText: {
  color: '#FFFFFF',
  fontSize: 16,
  fontWeight: '600',
},
deleteAccountButton: {
  marginTop: 28,
  borderWidth: 1,
  borderColor: '#FF7777',
  borderRadius: 12,
  paddingVertical: 16,
  flexDirection: 'row',
  justifyContent: 'center',
  alignItems: 'center',
  gap: 10,
},

deleteAccountButtonText: {
  color: '#FF7777',
  fontSize: 14,
  fontWeight: '800',
  letterSpacing: 1,
},

deleteOverlay: {
  flex: 1,
  backgroundColor: 'rgba(0,0,0,0.85)',
  justifyContent: 'center',
  padding: 24,
},

deleteCard: {
  backgroundColor: '#1C1E1C',
  borderRadius: 20,
  padding: 24,
  borderWidth: 1,
  borderColor: '#303330',
},

deleteHeader: {
  flexDirection: 'row',
  alignItems: 'center',
  justifyContent: 'space-between',
  marginBottom: 20,
},

deleteTitle: {
  color: '#FF7777',
  fontSize: 19,
  fontWeight: '800',
  letterSpacing: 1,
},

deleteWarning: {
  color: '#CCCCCC',
  fontSize: 14,
  lineHeight: 22,
  marginBottom: 24,
},

deleteLabel: {
  color: '#999E99',
  fontSize: 12,
  fontWeight: '800',
  marginBottom: 10,
},

deleteInput: {
  backgroundColor: '#0C0D0C',
  borderWidth: 1,
  borderColor: '#444944',
  borderRadius: 12,
  padding: 15,
  color: '#FFFFFF',
  fontSize: 16,
  marginBottom: 20,
},

confirmDeleteButton: {
  backgroundColor: '#B42323',
  borderRadius: 12,
  paddingVertical: 16,
  alignItems: 'center',
},

confirmDeleteText: {
  color: '#FFFFFF',
  fontSize: 14,
  fontWeight: '800',
  letterSpacing: 1,
},


});
