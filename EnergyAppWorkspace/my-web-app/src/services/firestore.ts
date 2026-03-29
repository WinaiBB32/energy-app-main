// src/services/firestore.ts
// Base Firestore service helpers

import {
  collection,
  doc,
  addDoc,
  updateDoc,
  deleteDoc,
  getDoc,
  getDocs,
  query,
  orderBy,
  serverTimestamp,
  type CollectionReference,
  type DocumentReference,
  type Query,
  type QuerySnapshot,
  type DocumentSnapshot,
  type OrderByDirection,
} from 'firebase/firestore'
import { db } from '@/firebase/config'

/**
 * Get a typed collection reference.
 */
export function getCollection<T = Record<string, unknown>>(
  collectionPath: string,
): CollectionReference<T> {
  return collection(db, collectionPath) as CollectionReference<T>
}

/**
 * Get a typed document reference.
 */
export function getDocRef<T = Record<string, unknown>>(
  collectionPath: string,
  docId: string,
): DocumentReference<T> {
  return doc(db, collectionPath, docId) as DocumentReference<T>
}

/**
 * Fetch a single document by ID. Returns null if not found.
 */
export async function fetchDoc<T>(
  collectionPath: string,
  docId: string,
): Promise<(T & { id: string }) | null> {
  const snap: DocumentSnapshot = await getDoc(doc(db, collectionPath, docId))
  if (!snap.exists()) return null
  return { id: snap.id, ...(snap.data() as T) }
}

/**
 * Fetch all documents in a collection, optionally ordered.
 */
export async function fetchAll<T>(
  collectionPath: string,
  orderByField?: string,
  direction: OrderByDirection = 'asc',
): Promise<(T & { id: string })[]> {
  let q: Query = collection(db, collectionPath)
  if (orderByField) {
    q = query(q, orderBy(orderByField, direction))
  }
  const snap: QuerySnapshot = await getDocs(q)
  return snap.docs.map((d) => ({ id: d.id, ...(d.data() as T) }))
}

/**
 * Add a new document to a collection with a server timestamp.
 * Returns the new document's ID.
 */
export async function addDocument<T extends Record<string, unknown>>(
  collectionPath: string,
  data: T,
): Promise<string> {
  const ref = await addDoc(collection(db, collectionPath), {
    ...data,
    createdAt: serverTimestamp(),
  })
  return ref.id
}

/**
 * Update an existing document by ID.
 */
export async function updateDocument<T extends Record<string, unknown>>(
  collectionPath: string,
  docId: string,
  data: Partial<T>,
): Promise<void> {
  await updateDoc(doc(db, collectionPath, docId), {
    ...data,
    updatedAt: serverTimestamp(),
  })
}

/**
 * Delete a document by ID.
 */
export async function deleteDocument(collectionPath: string, docId: string): Promise<void> {
  await deleteDoc(doc(db, collectionPath, docId))
}
